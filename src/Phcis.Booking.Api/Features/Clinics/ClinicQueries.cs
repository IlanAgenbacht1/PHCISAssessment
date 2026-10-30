using Microsoft.EntityFrameworkCore;
using Phcis.Booking.Api.Data;
using Phcis.Booking.Api.Infrastructure;
using Phcis.Booking.Domain.Appointments;
using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.Api.Features.Clinics;

public interface IClinicQueries
{
    Task<IReadOnlyList<ClinicDto>> GetClinicsAsync(CancellationToken ct);

    /// <summary>Free, future slots for one clinic-local day.</summary>
    Task<IReadOnlyList<TimeSlotDto>> GetAvailableSlotsAsync(int clinicId, DateOnly date, CancellationToken ct);
}

internal sealed class ClinicQueries(BookingDbContext db, TimeProvider time) : IClinicQueries
{
    public async Task<IReadOnlyList<ClinicDto>> GetClinicsAsync(CancellationToken ct) =>
        await db.Clinics
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new ClinicDto(c.Id, c.Name, c.Address, c.PhoneNumber))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TimeSlotDto>> GetAvailableSlotsAsync(int clinicId, DateOnly date, CancellationToken ct)
    {
        var clinic = await db.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, ct)
            ?? throw new NotFoundException("Clinic");

        var zone = clinic.TimeZone;
        var (dayStartUtc, dayEndUtc) = ClinicTime.DayBoundsUtc(date, zone);
        var nowUtc = time.GetUtcNow().UtcDateTime;
        var fromUtc = dayStartUtc > nowUtc ? dayStartUtc : nowUtc;

        var slots = await db.TimeSlots
            .AsNoTracking()
            .Where(s => s.ClinicId == clinicId && s.StartUtc > fromUtc && s.StartUtc < dayEndUtc)
            .Where(s => !db.Appointments.Any(a =>
                a.TimeSlotId == s.Id && AppointmentStatusExtensions.SlotHoldingStatuses.Contains(a.Status)))
            .OrderBy(s => s.StartUtc)
            .Select(s => new { s.Id, s.StartUtc, s.EndUtc })
            .ToListAsync(ct);

        return slots.Select(s => new TimeSlotDto(s.Id, s.StartUtc.ToClinicTime(zone), s.EndUtc.ToClinicTime(zone))).ToList();
    }
}
