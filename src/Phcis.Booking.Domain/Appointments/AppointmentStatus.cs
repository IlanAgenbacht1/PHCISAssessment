namespace Phcis.Booking.Domain.Appointments;

public enum AppointmentStatus
{
    Booked,
    Cancelled,
    Rescheduled,
    Attended,
    NoShow
}

public static class AppointmentStatusExtensions
{
    /// <summary>
    /// Whether an appointment in this state still occupies its slot. Cancelled and rescheduled ones
    /// free it. This needs to match the filter on the unique index in the database.
    /// </summary>
    public static bool HoldsSlot(this AppointmentStatus status) =>
        status is AppointmentStatus.Booked or AppointmentStatus.Attended or AppointmentStatus.NoShow;
}
