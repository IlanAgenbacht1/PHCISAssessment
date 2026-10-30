using FluentValidation;
using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.Shared.Validation;

public sealed class GenerateSlotsRequestValidator : AbstractValidator<GenerateSlotsRequest>
{
    public const int MaxDaysAhead = 92;

    public GenerateSlotsRequestValidator(TimeProvider timeProvider)
    {
        var today = () => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

        RuleFor(x => x.From).Must(from => from >= today()).WithMessage("Can't create slots in the past.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("End date must be on or after the start date.");
        RuleFor(x => x.To)
            .Must((request, to) => to.DayNumber - request.From.DayNumber < MaxDaysAhead)
            .WithMessage($"Generate at most {MaxDaysAhead} days at a time.");

        RuleFor(x => x.SlotMinutes).InclusiveBetween(5, 120);
        RuleFor(x => x.DayEnd).GreaterThan(x => x.DayStart).WithMessage("Day must end after it starts.");
        RuleFor(x => x)
            .Must(x => (x.DayEnd - x.DayStart).TotalMinutes >= x.SlotMinutes)
            .When(x => x.DayEnd > x.DayStart && x.SlotMinutes > 0)
            .WithName("SlotMinutes")
            .WithMessage("The day is shorter than one slot.");

        RuleFor(x => x.Days).NotEmpty().WithMessage("Pick at least one weekday.");
    }
}
