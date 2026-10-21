using LeaveManagement.Api.Data;
using LeaveManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Api.Services;

// Business rules and data access for leave requests. Uses LeaveDbContext directly:
// EF already is the data-access layer, so there is no repository on top of it.
public class LeaveRequestService
{
    private readonly LeaveDbContext _db;

    public LeaveRequestService(LeaveDbContext db)
    {
        _db = db;
    }

    public List<LeaveRequest> GetAll()
    {
        return _db.LeaveRequests
            .Include(r => r.Employee)
            .OrderByDescending(r => r.StartDate)
            .ToList();
    }

    public List<LeaveRequest> Search(string name)
    {
        // LINQ sends the name as a parameter, so it is only ever compared as text.
        // Concatenating it into raw SQL let the caller rewrite the query (SQL injection).
        return _db.LeaveRequests
            .Where(r => r.Employee!.Name.Contains(name))
            .ToList();
    }

    public ServiceResult<LeaveRequest> Create(CreateLeaveRequestDto dto)
    {
        var employee = _db.Employees.FirstOrDefault(e => e.Id == dto.EmployeeId);
        if (employee == null)
            return ServiceResult<LeaveRequest>.NotFound("Employee not found");

        // A negative range would subtract from the used days once approved.
        if (dto.EndDate.Date < dto.StartDate.Date)
            return ServiceResult<LeaveRequest>.BadRequest("End date must not be before start date");

        // Leave is counted in calendar days, so ignore any time-of-day component.
        var days = (dto.EndDate.Date - dto.StartDate.Date).Days + 1;

        // Pending requests count too, otherwise several pending requests could each
        // fit the quota on their own but exceed it together.
        if (dto.Type == LeaveType.Vacation)
        {
            var used = VacationDaysUsed(dto.EmployeeId, dto.StartDate.Year, includePending: true);
            if (used + days > employee.AnnualQuota)
                return ServiceResult<LeaveRequest>.BadRequest("Not enough vacation balance");
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

        return ServiceResult<LeaveRequest>.Ok(request);
    }

    public ServiceResult<LeaveRequest> Approve(int id)
    {
        // Returning before Commit() rolls the transaction back and releases the lock.
        using var transaction = _db.Database.BeginTransaction();

        var employeeId = _db.LeaveRequests
            .Where(r => r.Id == id)
            .Select(r => (int?)r.EmployeeId)
            .FirstOrDefault();
        if (employeeId == null)
            return ServiceResult<LeaveRequest>.NotFound("Leave request not found");

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
            return ServiceResult<LeaveRequest>.Conflict($"Leave request is already {request.Status}");

        // Only approved days count here: this request is pending itself, and other
        // pending requests have not been granted yet.
        if (request.Type == LeaveType.Vacation)
        {
            var approved = VacationDaysUsed(
                request.EmployeeId, request.StartDate.Year, includePending: false, excludeRequestId: request.Id);
            if (approved + request.Days > employee.AnnualQuota)
                return ServiceResult<LeaveRequest>.BadRequest("Not enough vacation balance");
        }

        request.Status = LeaveStatus.Approved;
        _db.SaveChanges();
        transaction.Commit();

        return ServiceResult<LeaveRequest>.Ok(request);
    }

    // Vacation days the employee has in the given year (by start date).
    // The single place the quota rule reads from, for both Create and Approve.
    private int VacationDaysUsed(int employeeId, int year, bool includePending, int? excludeRequestId = null)
    {
        return _db.LeaveRequests
            .Where(r => r.EmployeeId == employeeId
                        && r.Id != excludeRequestId
                        && r.Type == LeaveType.Vacation
                        && (r.Status == LeaveStatus.Approved
                            || (includePending && r.Status == LeaveStatus.Pending))
                        && r.StartDate.Year == year)
            .Sum(r => r.Days);
    }
}
