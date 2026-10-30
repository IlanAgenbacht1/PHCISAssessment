using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Phcis.Booking.Api.Data;
using Phcis.Booking.Api.Infrastructure;
using Phcis.Booking.Domain.Patients;
using Phcis.Booking.Shared;
using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.Api.Features.Account;

/// <summary>
/// Cookie-based auth for the WASM client, which is served from the same origin as this API.
/// See docs/03-decisions.md for why this isn't JWT.
/// </summary>
internal static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Account).WithTags("Account");

        group.MapPost("/register", Register)
            .Validate<RegisterPatientRequest>()
            .RequireRateLimiting(Policies.AuthRateLimit);

        group.MapPost("/login", Login)
            .Validate<LoginRequest>()
            .RequireRateLimiting(Policies.AuthRateLimit);

        group.MapPost("/logout", Logout).RequireAuthorization();
        // 204 rather than 401 when signed out: being anonymous is a normal answer to "who am I?",
        // not an error, and it keeps a red 401 out of the browser console on every first visit.
        group.MapGet("/me", Results<Ok<UserInfo>, NoContent> (ClaimsPrincipal user) =>
            user.Identity?.IsAuthenticated == true ? TypedResults.Ok(ToUserInfo(user)) : TypedResults.NoContent());
    }

    private static async Task<Results<Ok<UserInfo>, ValidationProblem, ProblemHttpResult>> Register(
        RegisterPatientRequest request,
        UserManager<IdentityUser> users,
        SignInManager<IdentityUser> signIn,
        BookingDbContext db,
        CancellationToken ct)
    {
        var identityType = request.IdentityType.ToDomain();
        var identityNumber = request.IdentityNumber.Trim().ToUpperInvariant();

        // Someone registered at reception already has a patient record. Linking it to a new online
        // account needs identity verification we don't have yet, so for now send them to the clinic.
        if (await db.Patients.AnyAsync(p => p.IdentityType == identityType && p.IdentityNumber == identityNumber, ct))
        {
            return TypedResults.Problem(
                "There's already a patient record for this ID number. Please ask at your clinic to link it to an online account.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // UserManager shares this DbContext, so the user, role, patient and claims commit or fail together.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var user = new IdentityUser { UserName = request.Email.Trim(), Email = request.Email.Trim() };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return TypedResults.ValidationProblem(created.Errors
                .GroupBy(e => e.Code.Contains("Password") ? nameof(request.Password) : nameof(request.Email))
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
        }

        await users.AddToRoleAsync(user, Roles.Patient);

        var patient = new Patient(user.Id, request.FirstName, request.LastName, identityType, identityNumber,
            request.DateOfBirth!.Value, request.PhoneNumber, request.Email);
        db.Patients.Add(patient);
        await db.SaveChangesAsync(ct);

        await users.AddClaimsAsync(user,
        [
            new Claim(AppClaims.PatientId, patient.Id.ToString()),
            new Claim(AppClaims.DisplayName, patient.FullName)
        ]);

        await transaction.CommitAsync(ct);

        await signIn.SignInAsync(user, isPersistent: false);
        return TypedResults.Ok(ToUserInfo(await signIn.CreateUserPrincipalAsync(user)));
    }

    private static async Task<Results<Ok<UserInfo>, ProblemHttpResult>> Login(
        LoginRequest request,
        UserManager<IdentityUser> users,
        SignInManager<IdentityUser> signIn)
    {
        var result = await signIn.PasswordSignInAsync(request.Email.Trim(), request.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return TypedResults.Problem("Too many failed attempts. Please try again in 15 minutes.", statusCode: StatusCodes.Status401Unauthorized);
        if (!result.Succeeded)
            return TypedResults.Problem("Email or password is incorrect.", statusCode: StatusCodes.Status401Unauthorized);

        // The new cookie only applies from the next request, so build the response from the user directly.
        var user = await users.FindByNameAsync(request.Email.Trim());
        return TypedResults.Ok(ToUserInfo(await signIn.CreateUserPrincipalAsync(user!)));
    }

    private static async Task<NoContent> Logout(SignInManager<IdentityUser> signIn)
    {
        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static UserInfo ToUserInfo(ClaimsPrincipal user)
    {
        var email = user.FindFirstValue(ClaimTypes.Name) ?? "";
        return new UserInfo(
            email,
            user.FindFirstValue(AppClaims.DisplayName) ?? email,
            user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
            user.FindInt(AppClaims.ClinicId));
    }
}
