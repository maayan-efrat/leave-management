using LeaveManagement.Api.Controllers;
using LeaveManagement.Api.Models;
using LeaveManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        var controller = new LeaveRequestsController(new LeaveRequestService(db));

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

        var controller = new LeaveRequestsController(new LeaveRequestService(db));

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

        var controller = new LeaveRequestsController(new LeaveRequestService(db));

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

        var controller = new LeaveRequestsController(new LeaveRequestService(db));

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

        var controller = new LeaveRequestsController(new LeaveRequestService(db));

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

        var controller = new LeaveRequestsController(new LeaveRequestService(db));

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

        var controller = new LeaveRequestsController(new LeaveRequestService(db));

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

    [Fact]
    public void Search_ByName_ReturnsOnlyThatEmployeesRequests()
    {
        // Arrange
        using var db = _database.NewDb();
        var dana = new Employee { Name = "Dana Levi", AnnualQuota = 20 };
        var yossi = new Employee { Name = "Yossi Cohen", AnnualQuota = 14 };
        db.Employees.AddRange(dana, yossi);
        db.SaveChanges();

        db.LeaveRequests.AddRange(
            new LeaveRequest { EmployeeId = dana.Id, Type = LeaveType.Vacation, StartDate = new DateTime(2026, 3, 1), EndDate = new DateTime(2026, 3, 1), Days = 1 },
            new LeaveRequest { EmployeeId = yossi.Id, Type = LeaveType.Vacation, StartDate = new DateTime(2026, 3, 1), EndDate = new DateTime(2026, 3, 1), Days = 1 });
        db.SaveChanges();

        var controller = new LeaveRequestsController(new LeaveRequestService(db));

        // Act
        var result = controller.Search("Dana");

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var found = Assert.Single(Assert.IsType<List<LeaveRequest>>(ok.Value));
        Assert.Equal(dana.Id, found.EmployeeId);
    }

    [Fact]
    public void Search_SqlInjection_IsTreatedAsPlainText()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Dana Levi", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        db.LeaveRequests.Add(new LeaveRequest { EmployeeId = emp.Id, Type = LeaveType.Vacation, StartDate = new DateTime(2026, 3, 1), EndDate = new DateTime(2026, 3, 1), Days = 1 });
        db.SaveChanges();

        var controller = new LeaveRequestsController(new LeaveRequestService(db));

        // Act: concatenated into the SQL, this turns the filter into "... OR '1'='1'".
        var result = controller.Search("' OR '1'='1");

        // Assert: no employee is called that, so nothing comes back.
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Empty(Assert.IsType<List<LeaveRequest>>(ok.Value));
    }

    [Fact]
    public void Approve_PendingRequest_Succeeds()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Approve Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var request = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3),
            Days = 3,
            Status = LeaveStatus.Pending
        };
        db.LeaveRequests.Add(request);
        db.SaveChanges();

        var controller = new LeaveRequestsController(new LeaveRequestService(db));

        // Act
        var result = controller.Approve(request.Id);

        // Assert: the new status is in the database, not only in the tracked entity.
        Assert.IsType<OkObjectResult>(result);
        db.ChangeTracker.Clear();
        Assert.Equal(LeaveStatus.Approved, db.LeaveRequests.Single(r => r.Id == request.Id).Status);
    }

    [Fact]
    public void Approve_MissingRequest_ReturnsNotFound()
    {
        // Arrange
        using var db = _database.NewDb();
        var controller = new LeaveRequestsController(new LeaveRequestService(db));

        // Act
        var result = controller.Approve(12345);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Theory]
    [InlineData(LeaveStatus.Approved)]
    [InlineData(LeaveStatus.Rejected)]
    public void Approve_AlreadyDecided_ReturnsConflict(LeaveStatus status)
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Decided Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var request = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3),
            Days = 3,
            Status = status
        };
        db.LeaveRequests.Add(request);
        db.SaveChanges();

        var controller = new LeaveRequestsController(new LeaveRequestService(db));

        // Act
        var result = controller.Approve(request.Id);

        // Assert
        Assert.IsType<ConflictObjectResult>(result);
        db.ChangeTracker.Clear();
        Assert.Equal(status, db.LeaveRequests.Single(r => r.Id == request.Id).Status);
    }

    [Fact]
    public void Approve_ExceedingQuota_ReturnsBadRequest()
    {
        // Arrange: 10 of 20 days approved, and a pending request for 15 more
        // (inserted directly, as a concurrent Create could have let through).
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Over Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 10),
            Days = 10,
            Status = LeaveStatus.Approved
        });
        var pending = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 15),
            Days = 15,
            Status = LeaveStatus.Pending
        };
        db.LeaveRequests.Add(pending);
        db.SaveChanges();

        var controller = new LeaveRequestsController(new LeaveRequestService(db));

        // Act: 10 + 15 = 25 > 20.
        var result = controller.Approve(pending.Id);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        db.ChangeTracker.Clear();
        Assert.Equal(LeaveStatus.Pending, db.LeaveRequests.Single(r => r.Id == pending.Id).Status);
    }

    [PostgresFact]
    public async Task Approve_ConcurrentApprovals_DoNotExceedQuota()
    {
        // Arrange: two pending requests (10 + 15 days) that each fit the 20-day
        // quota on their own but exceed it together.
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Race Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var first = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 10),
            Days = 10,
            Status = LeaveStatus.Pending
        };
        var second = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 5, 1),
            EndDate = new DateTime(2026, 5, 15),
            Days = 15,
            Status = LeaveStatus.Pending
        };
        db.LeaveRequests.AddRange(first, second);
        db.SaveChanges();

        // Approval #1 is in progress on its own connection: it holds the employee
        // lock and has marked the first request approved, but not committed yet.
        using var inProgress = _database.ConnectAgain();
        using var transaction = inProgress.Database.BeginTransaction();
        inProgress.Employees
            .FromSql($"SELECT * FROM \"Employees\" WHERE \"Id\" = {emp.Id} FOR UPDATE")
            .AsEnumerable()
            .Single();
        inProgress.LeaveRequests.Single(r => r.Id == first.Id).Status = LeaveStatus.Approved;
        inProgress.SaveChanges();

        // Act: approval #2 runs meanwhile on another connection.
        using var other = _database.ConnectAgain();
        var approveSecond = Task.Run(() => new LeaveRequestsController(new LeaveRequestService(other)).Approve(second.Id));

        // Assert: it waits for the lock instead of reading the uncommitted state...
        var finishedFirst = await Task.WhenAny(approveSecond, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.NotSame(approveSecond, finishedFirst);

        // ...and once approval #1 commits, it sees the 10 approved days and refuses.
        transaction.Commit();
        Assert.IsType<BadRequestObjectResult>(await approveSecond);

        db.ChangeTracker.Clear();
        Assert.Equal(LeaveStatus.Pending, db.LeaveRequests.Single(r => r.Id == second.Id).Status);
    }
}
