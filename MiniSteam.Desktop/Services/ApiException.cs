namespace MiniSteam.Desktop.Services;

public sealed class ApiException : Exception
{
    public int? StatusCode { get; }
    public string? Code { get; }
    public string? TraceId { get; }

    public ApiException(
        string message,
        int? statusCode = null,
        string? code = null,
        string? traceId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Code = code;
        TraceId = traceId;
    }
}
