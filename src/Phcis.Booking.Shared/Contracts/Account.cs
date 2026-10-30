namespace Phcis.Booking.Shared.Contracts;

public static class Roles
{
    public const string Patient = "Patient";
    public const string Staff = "Staff";
}

public enum IdentityDocumentType
{
    SaIdNumber,
    Passport
}

public sealed class RegisterPatientRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public IdentityDocumentType IdentityType { get; set; } = IdentityDocumentType.SaIdNumber;
    public string IdentityNumber { get; set; } = "";
    public DateOnly? DateOfBirth { get; set; }
    public string PhoneNumber { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed record UserInfo(string Email, string DisplayName, IReadOnlyList<string> Roles, int? ClinicId);
