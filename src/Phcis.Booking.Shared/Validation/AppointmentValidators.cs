using FluentValidation;
using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.Shared.Validation;

public sealed class BookAppointmentRequestValidator : AbstractValidator<BookAppointmentRequest>
{
    public BookAppointmentRequestValidator()
    {
        RuleFor(x => x.TimeSlotId).GreaterThan(0).WithMessage("Please choose a time.");
        RuleFor(x => x.ReasonForVisit).MaximumLength(250);
    }
}

public sealed class CancelAppointmentRequestValidator : AbstractValidator<CancelAppointmentRequest>
{
    public CancelAppointmentRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Please give a reason.").MaximumLength(250);
    }
}

public sealed class RescheduleAppointmentRequestValidator : AbstractValidator<RescheduleAppointmentRequest>
{
    public RescheduleAppointmentRequestValidator()
    {
        RuleFor(x => x.NewTimeSlotId).GreaterThan(0).WithMessage("Please choose a new time.");
    }
}
