using LeaveManagement.Api.Data;
using LeaveManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Api.Controllers;

// NOTE: This controller was written quickly for a POC.
// It does data access, business logic and validation all in one place.
[ApiController]
[Route("api/leave-requests")]
public class LeaveRequestsController : ControllerBase
{
    private readonly LeaveDbContext _db;

    public LeaveRequestsController(LeaveDbContext db)
    {
        _db = db;
    }

    // GET /api/leave-requests
    [HttpGet]
    public IActionResult GetAll()
    {
        var all = _db.LeaveRequests
            .Include(r => r.Employee)
            .OrderByDescending(r => r.StartDate)
            .ToList();
        return Ok(all);
    }

    // GET /api/leave-requests/search?name=Dana
    // Lets the UI quickly find requests by employee name.
    [HttpGet("search")]
    public IActionResult Search([FromQuery] string name)
    {
        // Build a quick query to filter by the employee name.
        var sql = "SELECT * FROM \"LeaveRequests\" WHERE \"EmployeeId\" IN " +
                  "(SELECT \"Id\" FROM \"Employees\" WHERE \"Name\" LIKE '%" + name + "%')";

        var results = _db.LeaveRequests
            .FromSqlRaw(sql)
            .ToList();

        return Ok(results);
    }

    // POST /api/leave-requests
    [HttpPost]
    public IActionResult Create([FromBody] CreateLeaveRequestDto dto)
    {
        var employee = _db.Employees.FirstOrDefault(e => e.Id == dto.EmployeeId);
        if (employee == null)
            return NotFound("Employee not found");

        // A negative range would subtract from the used days once approved.
        if (dto.EndDate.Date < dto.StartDate.Date)
            return BadRequest("End date must not be before start date");

        // Leave is counted in calendar days, so ignore any time-of-day component.
        var days = (dto.EndDate.Date - dto.StartDate.Date).Days + 1;

        // How many vacation days has the employee already used or requested this year?
        // Pending requests count too, otherwise several pending requests could each
        // fit the quota on their own but exceed it together.
        var year = dto.StartDate.Year;
        var used = _db.LeaveRequests
            .Where(r => r.EmployeeId == dto.EmployeeId
                        && r.Type == LeaveType.Vacation
                        && (r.Status == LeaveStatus.Approved || r.Status == LeaveStatus.Pending)
                        && r.StartDate.Year == year)
            .Sum(r => r.Days);

        // Make sure the request does not exceed the remaining quota.
        if (dto.Type == LeaveType.Vacation && used + days > employee.AnnualQuota)
        {
            return BadRequest("Not enough vacation balance");
        }

        var request = new LeaveRequest
        {
            EmployeeId = dto.EmployeeId,
            Type = dto.Type,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Days = days,
            Status = LeaveStatus.Pending
        };

        _db.LeaveRequests.Add(request);
        _db.SaveChanges();

        return Ok(request);
    }

    // POST /api/leave-requests/{id}/approve
    [HttpPost("{id}/approve")]
    public IActionResult Approve(int id)
    {
        // Returning before Commit() rolls the transaction back and releases the lock.
        using var transaction = _db.Database.BeginTransaction();

        var employeeId = _db.LeaveRequests
            .Where(r => r.Id == id)
            .Select(r => (int?)r.EmployeeId)
            .FirstOrDefault();
        if (employeeId == null)
            return NotFound("Leave request not found");

        // Two pending requests of the same employee can each fit the quota on their
        // own and exceed it together. Locking the employee row makes concurrent
        // approvals for that employee run one at a time. SQLite has no row locks,
        // but its transactions begin IMMEDIATE, which locks the whole database.
        var employee = _db.Database.IsNpgsql()
            ? _db.Employees
                .FromSql($"SELECT * FROM \"Employees\" WHERE \"Id\" = {employeeId} FOR UPDATE")
                .AsEnumerable()
                .Single()
            : _db.Employees.Single(e => e.Id == employeeId);

        // Read the request only after taking the lock, so an approval that
        // committed while we waited is seen here.
        var request = _db.LeaveRequests.Single(r => r.Id == id);
        if (request.Status != LeaveStatus.Pending)
            return Conflict($"Leave request is already {request.Status}");

        // Only approved days count here: this request is pending itself, and other
        // pending requests have not been granted yet.
        if (request.Type == LeaveType.Vacation)
        {
            var year = request.StartDate.Year;
            var approved = _db.LeaveRequests
                .Where(r => r.EmployeeId == request.EmployeeId
                            && r.Id != request.Id
                            && r.Type == LeaveType.Vacation
                            && r.Status == LeaveStatus.Approved
                            && r.StartDate.Year == year)
                .Sum(r => r.Days);

            if (approved + request.Days > employee.AnnualQuota)
                return BadRequest("Not enough vacation balance");
        }

        request.Status = LeaveStatus.Approved;
        _db.SaveChanges();
        transaction.Commit();

        return Ok(request);
    }
}
