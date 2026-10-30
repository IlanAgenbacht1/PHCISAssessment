using FluentValidation;
using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.Shared.Validation;

public sealed class RegisterPatientRequestValidator : AbstractValidator<RegisterPatientRequest>
{
    public RegisterPatientRequestValidator(TimeProvider timeProvider)
    {
        var today = () => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);

        RuleFor(x => x.IdentityNumber).NotEmpty().WithMessage("ID or passport number is required.");

        When(x => x.IdentityType == IdentityDocumentType.SaIdNumber, () =>
        {
            RuleFor(x => x.IdentityNumber)
                .Must(id => SaIdNumber.IsValid(id, today()))
                .When(x => !string.IsNullOrWhiteSpace(x.IdentityNumber))
                .WithMessage("That isn't a valid South African ID number.");

            RuleFor(x => x.DateOfBirth)
                .Must((request, dob) => SaIdNumber.TryGetDateOfBirth(request.IdentityNumber, today(), out var fromId) && fromId == dob)
                .When(x => x.DateOfBirth is not null && SaIdNumber.IsValid(x.IdentityNumber, today()))
                .WithMessage("Date of birth doesn't match the ID number.");
        });

        When(x => x.IdentityType == IdentityDocumentType.Passport, () =>
        {
            RuleFor(x => x.IdentityNumber)
                .Matches("^[A-Za-z0-9]{6,20}$")
                .WithMessage("Passport numbers are 6 to 20 letters or digits.");
        });

        RuleFor(x => x.DateOfBirth)
            .NotNull().WithMessage("Date of birth is required.")
            .Must(dob => dob <= today()).WithMessage("Date of birth can't be in the future.")
            .Must(dob => dob >= new DateOnly(1900, 1, 1)).WithMessage("Please check the date of birth.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .Matches(@"^(\+27|0)\d{9}$").WithMessage("Use a South African number, e.g. 0821234567 or +27821234567.");

        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);

        // Mirrors the Identity password options configured in the API, so users see the rule
        // before submitting instead of getting it back from the server.
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password needs an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password needs a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password needs a digit.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
