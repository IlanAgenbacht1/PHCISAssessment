using Phcis.Booking.Client.Auth;
using Phcis.Booking.Shared;
using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.Client.Services;

public interface IAccountApi
{
    Task<ApiResult<UserInfo>> LoginAsync(LoginRequest request);
    Task<ApiResult<UserInfo>> RegisterAsync(RegisterPatientRequest request);
    Task LogoutAsync();
}

public sealed class AccountApi(HttpClient http, CookieAuthenticationStateProvider auth) : ApiClientBase(http, auth), IAccountApi
{
    public async Task<ApiResult<UserInfo>> LoginAsync(LoginRequest request) =>
        SignInOnSuccess(await PostAsync<UserInfo>($"{ApiRoutes.Account}/login", request));

    public async Task<ApiResult<UserInfo>> RegisterAsync(RegisterPatientRequest request) =>
        SignInOnSuccess(await PostAsync<UserInfo>($"{ApiRoutes.Account}/register", request));

    public async Task LogoutAsync()
    {
        await PostAsync($"{ApiRoutes.Account}/logout", null);
        Auth.MarkSignedOut();
    }

    private ApiResult<UserInfo> SignInOnSuccess(ApiResult<UserInfo> result)
    {
        if (result.Succeeded) Auth.MarkSignedIn(result.Value);
        return result;
    }
}
