using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.Api.Infrastructure;

internal static class Policies
{
    public const string Patient = nameof(Patient);
    public const string Staff = nameof(Staff);
    public const string AuthRateLimit = "auth";

    public static void AddBookingPolicies(this IServiceCollection services) =>
        services.AddAuthorizationBuilder()
            // Requiring the claim as well as the role means handlers can rely on it being there.
            .AddPolicy(Patient, p => p.RequireRole(Roles.Patient).RequireClaim(AppClaims.PatientId))
            .AddPolicy(Staff, p => p.RequireRole(Roles.Staff).RequireClaim(AppClaims.ClinicId));
}
