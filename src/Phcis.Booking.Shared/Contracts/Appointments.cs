namespace Phcis.Booking.Shared.Contracts;

public enum AppointmentStatus
{
    Booked,
    Cancelled,
    Rescheduled,
    Attended,
    NoShow
}

public sealed record AppointmentDto(
    int Id,
    string Reference,
    int ClinicId,
    string ClinicName,
    string ClinicAddress,
    DateTimeOffset Start,
    DateTimeOffset End,
    AppointmentStatus Status,
    string? ReasonForVisit,
    string? CancellationReason,
    bool CanBeChanged);

public sealed class BookAppointmentRequest
{
    public int TimeSlotId { get; set; }
    public string? ReasonForVisit { get; set; }
}

public sealed class CancelAppointmentRequest
{
    public string Reason { get; set; } = "";
}

public sealed class RescheduleAppointmentRequest
{
    public int NewTimeSlotId { get; set; }
}
