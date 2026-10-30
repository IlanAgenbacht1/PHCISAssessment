using Phcis.Booking.Domain.Patients;
using Phcis.Booking.Domain.Scheduling;

namespace Phcis.Booking.Domain.Appointments;

/// <summary>
/// Appointments are never deleted. Every state change is kept for audit purposes, and
/// a reschedule creates a new appointment that points back at the one it replaced.
/// </summary>
public class Appointment
{
    public const int MaxReasonLength = 250;

    private Appointment() { }

    public int Id { get; private set; }
    public string Reference { get; private set; } = null!;
    public int PatientId { get; private set; }
    public Patient Patient { get; private set; } = null!;
    public int TimeSlotId { get; private set; }
    public TimeSlot TimeSlot { get; private set; } = null!;
    public AppointmentStatus Status { get; private set; }
    public string? ReasonForVisit { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime? CancelledUtc { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime? OutcomeRecordedUtc { get; private set; }
    public int? RescheduledFromId { get; private set; }

    public bool CanBeChanged(DateTime nowUtc) => Status == AppointmentStatus.Booked && !TimeSlot.HasStarted(nowUtc);

    /// <summary>
    /// Checks the rules we can check in memory. Whether the slot is still free is the database's job;
    /// checking it here would just be a race.
    /// </summary>
    public static Appointment Book(int patientId, TimeSlot slot, string? reasonForVisit, DateTime nowUtc)
    {
        if (slot.HasStarted(nowUtc))
            throw new BookingRuleException("That slot is in the past.");

        return new Appointment
        {
            Reference = BookingReference.New(),
            PatientId = patientId,
            TimeSlotId = slot.Id,
            TimeSlot = slot,
            Status = AppointmentStatus.Booked,
            ReasonForVisit = string.IsNullOrWhiteSpace(reasonForVisit) ? null : reasonForVisit.Trim(),
            CreatedUtc = nowUtc
        };
    }

    public void Cancel(string reason, DateTime nowUtc)
    {
        EnsureChangeable(nowUtc, "cancelled");
        Status = AppointmentStatus.Cancelled;
        CancelledUtc = nowUtc;
        CancellationReason = reason.Trim();
    }

    /// <summary>
    /// Marks this appointment as rescheduled and returns the replacement. Both changes need to be
    /// saved in one transaction so a failed booking of the new slot leaves this one untouched.
    /// </summary>
    public Appointment RescheduleTo(TimeSlot newSlot, DateTime nowUtc)
    {
        EnsureChangeable(nowUtc, "rescheduled");
        if (newSlot.Id == TimeSlotId)
            throw new BookingRuleException("Pick a different time to reschedule to.");
        if (newSlot.ClinicId != TimeSlot.ClinicId)
            throw new BookingRuleException("Appointments can only be moved within the same clinic.");

        var replacement = Book(PatientId, newSlot, ReasonForVisit, nowUtc);
        replacement.RescheduledFromId = Id;

        Status = AppointmentStatus.Rescheduled;
        CancelledUtc = nowUtc;
        CancellationReason = $"Rescheduled to {replacement.Reference}";

        return replacement;
    }

    public void RecordAttendance(bool attended, DateTime nowUtc)
    {
        if (Status != AppointmentStatus.Booked)
            throw new BookingRuleException($"Attendance can't be recorded for a {Status} appointment.");
        if (!TimeSlot.HasStarted(nowUtc))
            throw new BookingRuleException("Attendance can only be recorded once the appointment has started.");

        Status = attended ? AppointmentStatus.Attended : AppointmentStatus.NoShow;
        OutcomeRecordedUtc = nowUtc;
    }

    private void EnsureChangeable(DateTime nowUtc, string action)
    {
        if (Status != AppointmentStatus.Booked)
            throw new BookingRuleException($"Only booked appointments can be {action}.");
        if (TimeSlot.HasStarted(nowUtc))
            throw new BookingRuleException($"This appointment has already started and can't be {action}.");
    }
}
