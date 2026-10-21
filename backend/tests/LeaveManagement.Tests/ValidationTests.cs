using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LeaveManagement.Tests;

// Input validation runs in the ASP.NET pipeline, not when a test calls the
// controller method directly, so these tests go through real HTTP.
public class ValidationTests : IClassFixture<ValidationTests.ApiFactory>
{
    private readonly HttpClient _client;

    public ValidationTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_UnknownLeaveType_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/leave-requests", new
        {
            employeeId = 1,
            type = 99,
            startDate = new DateTime(2026, 3, 1),
            endDate = new DateTime(2026, 3, 3)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // The request is rejected before it reaches the database, so a throwaway
    // SQLite file is enough for the app to start. Not :memory:, because startup
    // creates the schema and seeds it on separate connections.
    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"leave-validation-{Guid.NewGuid()}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Database:Provider", "Sqlite");
            builder.UseSetting("ConnectionStrings:LeaveSqlite", $"Data Source={_dbPath}");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            SqliteConnection.ClearAllPools();
            File.Delete(_dbPath);
        }
    }
}
