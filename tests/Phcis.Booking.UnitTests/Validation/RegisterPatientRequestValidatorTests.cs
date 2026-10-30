using Microsoft.Extensions.Time.Testing;
using Phcis.Booking.Shared.Contracts;
using Phcis.Booking.Shared.Validation;

namespace Phcis.Booking.UnitTests.Validation;

public class RegisterPatientRequestValidatorTests
{
    private readonly RegisterPatientRequestValidator _validator =
        new(new FakeTimeProvider(new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero)));

    private static RegisterPatientRequest ValidRequest() => new()
    {
        FirstName = "Thandi",
        LastName = "Mokoena",
        IdentityType = IdentityDocumentType.SaIdNumber,
        IdentityNumber = "9506150123088",
        DateOfBirth = new DateOnly(1995, 6, 15),
        PhoneNumber = "0821234567",
        Email = "thandi@example.com",
        Password = "Clinic2026"
    };

    [Fact]
    public void Valid_request_passes()
    {
        Assert.True(_validator.Validate(ValidRequest()).IsValid);
    }

    [Fact]
    public void Date_of_birth_must_match_sa_id()
    {
        var request = ValidRequest();
        request.DateOfBirth = new DateOnly(1995, 6, 16);

        var result = _validator.Validate(request);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(request.DateOfBirth) && e.ErrorMessage.Contains("match"));
    }

    [Fact]
    public void Invalid_sa_id_reports_on_identity_number_only()
    {
        var request = ValidRequest();
        request.IdentityNumber = "9506150123089";

        var result = _validator.Validate(request);

        Assert.Single(result.Errors);
        Assert.Equal(nameof(request.IdentityNumber), result.Errors[0].PropertyName);
    }

    [Fact]
    public void Passport_skips_sa_id_checks()
    {
        var request = ValidRequest();
        request.IdentityType = IdentityDocumentType.Passport;
        request.IdentityNumber = "A12345678";
        request.DateOfBirth = new DateOnly(1988, 2, 3);

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Theory]
    [InlineData("0821234567")]
    [InlineData("+27821234567")]
    public void Accepts_sa_phone_formats(string phone)
    {
        var request = ValidRequest();
        request.PhoneNumber = phone;

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Theory]
    [InlineData("082 123 4567")]
    [InlineData("12345")]
    [InlineData("+44821234567")]
    public void Rejects_other_phone_formats(string phone)
    {
        var request = ValidRequest();
        request.PhoneNumber = phone;

        Assert.False(_validator.Validate(request).IsValid);
    }

    [Theory]
    [InlineData("short1A")]
    [InlineData("alllowercase1")]
    [InlineData("NoDigitsHere")]
    public void Rejects_weak_passwords(string password)
    {
        var request = ValidRequest();
        request.Password = password;

        Assert.False(_validator.Validate(request).IsValid);
    }
}
