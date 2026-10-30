using System.Security.Claims;

namespace Phcis.Booking.Api.Infrastructure;

internal static class AppClaims
{
    public const string PatientId = "phcis:patient_id";
    public const string ClinicId = "phcis:clinic_id";
    public const string DisplayName = "phcis:display_name";

    public static int GetPatientId(this ClaimsPrincipal user) => GetRequiredInt(user, PatientId);

    public static int GetClinicId(this ClaimsPrincipal user) => GetRequiredInt(user, ClinicId);

    public static int? FindInt(this ClaimsPrincipal user, string type) =>
        int.TryParse(user.FindFirstValue(type), out var value) ? value : null;

    // The role checks on each endpoint group should make a missing claim impossible. If it
    // happens anyway, it's a data problem (a staff account with no clinic) and should be loud.
    private static int GetRequiredInt(ClaimsPrincipal user, string type) =>
        user.FindInt(type) ?? throw new InvalidOperationException($"Signed-in user has no {type} claim.");
}
