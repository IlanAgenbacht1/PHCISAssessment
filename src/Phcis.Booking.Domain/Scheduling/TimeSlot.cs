using Phcis.Booking.Domain.Clinics;

namespace Phcis.Booking.Domain.Scheduling;

/// <summary>
/// A bookable window at a clinic. Slots are stored as rows (generated ahead of time by staff)
/// rather than calculated on the fly, so a booking has a real row to point at. That's what
/// lets the database stop double bookings.
/// </summary>
public class TimeSlot
{
    private TimeSlot() { }

    public TimeSlot(int clinicId, DateTime startUtc, DateTime endUtc)
    {
        if (startUtc.Kind != DateTimeKind.Utc || endUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Slot times must be UTC.");
        if (endUtc <= startUtc)
            throw new ArgumentException("Slot must end after it starts.");

        ClinicId = clinicId;
        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public int Id { get; private set; }
    public int ClinicId { get; private set; }
    public Clinic Clinic { get; private set; } = null!;
    public DateTime StartUtc { get; private set; }
    public DateTime EndUtc { get; private set; }

    public bool HasStarted(DateTime nowUtc) => StartUtc <= nowUtc;

    public bool Overlaps(DateTime startUtc, DateTime endUtc) => StartUtc < endUtc && startUtc < EndUtc;
}
