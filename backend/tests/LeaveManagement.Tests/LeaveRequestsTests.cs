using LeaveManagement.Api.Controllers;
using LeaveManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace LeaveManagement.Tests;

public class LeaveRequestsTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _database;

    public LeaveRequestsTests(TestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public void Create_WithinQuota_Succeeds()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act: request 3 days, well within the quota.
        var result = controller.Create(new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3)
        });

        // Assert
        Assert.IsType<OkObjectResult>(result);
        Assert.Single(db.LeaveRequests);
    }

    [Fact]
    public void Create_ExceedingRemainingQuota_ReturnsBadRequest()
    {
        // Arrange: employee has already used 18 of 20 days.
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Almost Out", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 1, 6),
            EndDate = new DateTime(2026, 1, 23),
            Days = 18,
            Status = LeaveStatus.Approved
        });
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act: request 3 more days — 18 + 3 = 21 > 20.
        var result = controller.Create(new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3)
        });

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(1, db.LeaveRequests.Count(r => r.EmployeeId == emp.Id));
    }

    [Fact]
    public void Create_UsingExactlyRemainingQuota_Succeeds()
    {
        // Arrange: employee has already used 18 of 20 days.
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Exactly Enough", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 1, 6),
            EndDate = new DateTime(2026, 1, 23),
            Days = 18,
            Status = LeaveStatus.Approved
        });
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act: request the last 2 days — 18 + 2 = 20.
        var result = controller.Create(new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 2)
        });

        // Assert
        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, db.LeaveRequests.Count(r => r.EmployeeId == emp.Id));
    }

    [Fact]
    public void Create_PendingRequestsCountTowardsQuota()
    {
        // Arrange: 15 of 20 days are already requested and still pending.
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Pending Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 2, 1),
            EndDate = new DateTime(2026, 2, 15),
            Days = 15,
            Status = LeaveStatus.Pending
        });
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act: request 6 more days — 15 + 6 = 21 > 20.
        var result = controller.Create(new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 6)
        });

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(1, db.LeaveRequests.Count(r => r.EmployeeId == emp.Id));
    }

    [Fact]
    public void Create_PreviousYearUsage_NotCounted()
    {
        // Arrange: 18 days were used last year.
        using var db = _database.NewDb();
        var emp = new Employee { Name = "New Year Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2025, 6, 1),
            EndDate = new DateTime(2025, 6, 18),
            Days = 18,
            Status = LeaveStatus.Approved
        });
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act: request 5 days this year.
        var result = controller.Create(new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 5)
        });

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void Create_EndBeforeStart_ReturnsBadRequest()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Backwards Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act: end date is 8 days before the start date.
        var result = controller.Create(new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 10),
            EndDate = new DateTime(2026, 3, 1)
        });

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(db.LeaveRequests);
    }

    [Fact]
    public void Create_IgnoresTimeOfDay_WhenCountingDays()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Night Shift Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act: 23:00 to 01:00 next day is two calendar days, not one.
        var result = controller.Create(new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 8, 1, 23, 0, 0),
            EndDate = new DateTime(2026, 8, 2, 1, 0, 0)
        });

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, Assert.IsType<LeaveRequest>(ok.Value).Days);
    }
}
