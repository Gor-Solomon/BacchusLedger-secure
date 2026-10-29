namespace BacchusLedger.Services;

/// Outcome of a service call: a value, or "not found" / "bad request" with an optional error text.
public sealed record ServiceResult<T>(ServiceStatus Status, T? Value, string? Error)
{
    public static ServiceResult<T> Ok(T value) => new(ServiceStatus.Ok, value, null);
    public static ServiceResult<T> NotFound(string? error = null) => new(ServiceStatus.NotFound, default, error);
    public static ServiceResult<T> BadRequest(string error) => new(ServiceStatus.BadRequest, default, error);

    public static implicit operator ServiceResult<T>(T value) => Ok(value);
}
