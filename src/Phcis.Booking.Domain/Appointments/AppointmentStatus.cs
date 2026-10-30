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
    /// States in which an appointment still occupies its slot. Cancelled and rescheduled ones free it.
    /// The database's unique index filter is built from this list, so the two can't disagree.
    /// </summary>
    public static readonly AppointmentStatus[] SlotHoldingStatuses =
        [AppointmentStatus.Booked, AppointmentStatus.Attended, AppointmentStatus.NoShow];

    public static bool HoldsSlot(this AppointmentStatus status) => SlotHoldingStatuses.Contains(status);
}
