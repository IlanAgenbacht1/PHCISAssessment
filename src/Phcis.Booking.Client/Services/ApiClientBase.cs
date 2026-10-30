using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Phcis.Booking.Client.Auth;
using Phcis.Booking.Shared;

namespace Phcis.Booking.Client.Services;

/// <summary>
/// Turns HTTP responses into <see cref="ApiResult"/>s so pages never deal with status codes or
/// exceptions for expected failures. A 401 anywhere means the session has expired, so the auth
/// state is reset and the router sends the user to sign in.
/// </summary>
public abstract class ApiClientBase(HttpClient http, CookieAuthenticationStateProvider auth)
{
    protected CookieAuthenticationStateProvider Auth { get; } = auth;

    protected Task<ApiResult<T>> GetAsync<T>(string url, CancellationToken ct = default) =>
        SendAsync<T>(() => http.GetAsync(url, ct), ct);

    protected Task<ApiResult<T>> PostAsync<T>(string url, object body, CancellationToken ct = default) =>
        SendAsync<T>(() => http.PostAsJsonAsync(url, body, SharedJson.Options, ct), ct);

    protected async Task<ApiResult> PostAsync(string url, object? body, CancellationToken ct = default)
    {
        using var response = await TrySendAsync(() => http.PostAsJsonAsync(url, body, SharedJson.Options, ct));
        if (response is null) return ApiResult.Failure(ApiError.NetworkFailure);
        return response.IsSuccessStatusCode ? ApiResult.Success() : ApiResult.Failure(await ReadErrorAsync(response, ct));
    }

    private async Task<ApiResult<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        using var response = await TrySendAsync(send);
        if (response is null) return ApiResult<T>.Failure(ApiError.NetworkFailure);
        if (!response.IsSuccessStatusCode) return ApiResult<T>.Failure(await ReadErrorAsync(response, ct));

        var value = await response.Content.ReadFromJsonAsync<T>(SharedJson.Options, ct);
        return ApiResult<T>.Success(value!);
    }

    private static async Task<HttpResponseMessage?> TrySendAsync(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            return await send();
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<ApiError> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            Auth.MarkSignedOut();

        ProblemResponse? problem = null;
        if (response.Content.Headers.ContentType?.MediaType?.Contains("json") == true)
        {
            try
            {
                problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(SharedJson.Options, ct);
            }
            catch (JsonException)
            {
                // Not the shape we expected. The default message for the status code will do.
            }
        }

        var message = problem?.Errors is { Count: > 0 } ? "Please check the highlighted fields." : problem?.Detail ?? problem?.Title;
        return ApiError.From(response.StatusCode, message, problem?.Errors);
    }

    private sealed record ProblemResponse(string? Title, string? Detail, Dictionary<string, string[]>? Errors);
}
