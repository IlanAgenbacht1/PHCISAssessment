using System.Net;

namespace Phcis.Booking.Client.Services;

public sealed record ApiError(HttpStatusCode Status, string Message, IReadOnlyDictionary<string, string[]> FieldErrors)
{
    private static readonly IReadOnlyDictionary<string, string[]> NoFieldErrors = new Dictionary<string, string[]>();

    public static ApiError NetworkFailure { get; } =
        new(0, "Couldn't reach the server. Check your connection and try again.", NoFieldErrors);

    public static ApiError From(HttpStatusCode status, string? message, IReadOnlyDictionary<string, string[]>? fieldErrors) =>
        new(status, message ?? DefaultMessage(status), fieldErrors ?? NoFieldErrors);

    public bool IsConflict => Status == HttpStatusCode.Conflict;
    public bool HasFieldErrors => FieldErrors.Count > 0;

    private static string DefaultMessage(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => "Please sign in again.",
        HttpStatusCode.Forbidden => "You don't have access to that.",
        HttpStatusCode.NotFound => "We couldn't find that.",
        HttpStatusCode.TooManyRequests => "Too many attempts. Wait a minute and try again.",
        _ => "Something went wrong on our side. Please try again."
    };
}

public class ApiResult
{
    protected ApiResult(ApiError? error) => Error = error;

    public ApiError? Error { get; }
    public bool Succeeded => Error is null;

    public static ApiResult Success() => new(null);
    public static ApiResult Failure(ApiError error) => new(error);
}

public sealed class ApiResult<T> : ApiResult
{
    private readonly T? _value;

    private ApiResult(T? value, ApiError? error) : base(error) => _value = value;

    public T Value => Succeeded ? _value! : throw new InvalidOperationException("No value on a failed result.");

    public static ApiResult<T> Success(T value) => new(value, null);
    public static new ApiResult<T> Failure(ApiError error) => new(default, error);
}
