namespace Phcis.Booking.Domain.Patients;

public enum IdentityDocumentType
{
    SaIdNumber,
    Passport
}

public class Patient
{
    private Patient() { }

    public Patient(
        string? userId,
        string firstName,
        string lastName,
        IdentityDocumentType identityType,
        string identityNumber,
        DateOnly dateOfBirth,
        string phoneNumber,
        string email)
    {
        UserId = userId;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        IdentityType = identityType;
        IdentityNumber = identityNumber.Trim().ToUpperInvariant();
        DateOfBirth = dateOfBirth;
        PhoneNumber = phoneNumber.Trim();
        Email = email.Trim();
    }

    public int Id { get; private set; }

    /// <summary>
    /// Link to the ASP.NET Identity account, kept as a plain id so the domain doesn't depend on Identity.
    /// Null for patients registered at reception who never signed up online.
    /// </summary>
    public string? UserId { get; private set; }

    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public IdentityDocumentType IdentityType { get; private set; }
    public string IdentityNumber { get; private set; } = null!;
    public DateOnly DateOfBirth { get; private set; }
    public string PhoneNumber { get; private set; } = null!;
    public string Email { get; private set; } = null!;

    public string FullName => $"{FirstName} {LastName}";
}
