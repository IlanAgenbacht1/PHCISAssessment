using Phcis.Booking.Domain.Scheduling;

namespace Phcis.Booking.UnitTests.Domain;

internal static class TestSlots
{
    public static readonly DateTime Now = new(2026, 11, 2, 7, 0, 0, DateTimeKind.Utc);

    /// <summary>Ids are normally assigned by the database; the rules compare them, so set one here.</summary>
    public static TimeSlot At(DateTime startUtc, int id = 1, int clinicId = 1, int minutes = 20)
    {
        var slot = new TimeSlot(clinicId, startUtc, startUtc.AddMinutes(minutes));
        typeof(TimeSlot).GetProperty(nameof(TimeSlot.Id))!.SetValue(slot, id);
        return slot;
    }

    public static TimeSlot InHours(double hours, int id = 1, int clinicId = 1) => At(Now.AddHours(hours), id, clinicId);
}
