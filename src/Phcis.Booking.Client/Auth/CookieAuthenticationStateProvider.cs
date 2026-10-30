using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Phcis.Booking.Shared;
using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.Client.Auth;

/// <summary>
/// The auth cookie is HttpOnly, so this code can't read it. That's the point. Instead it asks the API
/// who the cookie belongs to, and caches the answer until a sign-in, a sign-out or a 401 changes it.
/// </summary>
public sealed class CookieAuthenticationStateProvider(HttpClient http) : AuthenticationStateProvider
{
    public const string DisplayNameClaim = "display_name";
    public const string ClinicIdClaim = "clinic_id";

    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private Task<AuthenticationState>? _state;

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _state ??= LoadAsync();

    public void MarkSignedIn(UserInfo user) => SetState(new AuthenticationState(ToPrincipal(user)));

    public void MarkSignedOut() => SetState(Anonymous);

    private void SetState(AuthenticationState state)
    {
        _state = Task.FromResult(state);
        NotifyAuthenticationStateChanged(_state);
    }

    private async Task<AuthenticationState> LoadAsync()
    {
        try
        {
            using var response = await http.GetAsync($"{ApiRoutes.Account}/me");
            if (response.StatusCode != System.Net.HttpStatusCode.OK) return Anonymous;

            var user = await response.Content.ReadFromJsonAsync<UserInfo>(SharedJson.Options);
            return user is null ? Anonymous : new AuthenticationState(ToPrincipal(user));
        }
        catch (HttpRequestException)
        {
            return Anonymous;
        }
    }

    private static ClaimsPrincipal ToPrincipal(UserInfo user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Email),
            new(DisplayNameClaim, user.DisplayName)
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        if (user.ClinicId is { } clinicId)
            claims.Add(new Claim(ClinicIdClaim, clinicId.ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "cookie"));
    }
}
