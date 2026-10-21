namespace LeaveManagement.Api.Services;

public enum ServiceStatus
{
    Ok,
    NotFound,
    BadRequest,
    Conflict
}

// What a service operation returned, without knowing anything about HTTP.
// The controller decides which status code each outcome maps to.
public record ServiceResult<T>(ServiceStatus Status, T? Value = default, string? Error = null)
{
    public static ServiceResult<T> Ok(T value) => new(ServiceStatus.Ok, value);
    public static ServiceResult<T> NotFound(string error) => new(ServiceStatus.NotFound, Error: error);
    public static ServiceResult<T> BadRequest(string error) => new(ServiceStatus.BadRequest, Error: error);
    public static ServiceResult<T> Conflict(string error) => new(ServiceStatus.Conflict, Error: error);
}
