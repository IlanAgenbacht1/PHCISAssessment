using Microsoft.EntityFrameworkCore;
using Phcis.Booking.Api.Data;
using Phcis.Booking.Api.Data.Configurations;
using Phcis.Booking.Api.Infrastructure;
using Phcis.Booking.Domain;
using Phcis.Booking.Domain.Appointments;
using Phcis.Booking.Domain.Scheduling;
using Phcis.Booking.Shared.Contracts;
using AppointmentStatus = Phcis.Booking.Domain.Appointments.AppointmentStatus;

namespace Phcis.Booking.Api.Features.Staff;

/// <summary>Staff-facing operations. Every method is scoped to the staff member's own clinic.</summary>
public interface IClinicScheduleService
{
    Task<DayListDto> GetDayListAsync(int clinicId, DateOnly date, CancellationToken ct);
    Task RecordAttendanceAsync(int clinicId, int appointmentId, bool attended, CancellationToken ct);
    Task<GenerateSlotsResult> GenerateSlotsAsync(int clinicId, GenerateSlotsRequest request, CancellationToken ct);
}

internal sealed class ClinicScheduleService(
    BookingDbContext db,
    TimeProvider time,
    ILogger<ClinicScheduleService> logger) : IClinicScheduleService
{
    public async Task<DayListDto> GetDayListAsync(int clinicId, DateOnly date, CancellationToken ct)
    {
        var clinic = await db.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, ct)
            ?? throw new NotFoundException("Clinic");
        var zone = clinic.TimeZone;
        var (startUtc, endUtc) = ClinicTime.DayBoundsUtc(date, zone);
        var nowUtc = time.GetUtcNow().UtcDateTime;

        var totalSlots = await db.TimeSlots.CountAsync(
            s => s.ClinicId == clinicId && s.StartUtc >= startUtc && s.StartUtc < endUtc, ct);

        var rows = await db.Appointments
            .AsNoTracking()
            .Where(a => a.TimeSlot.ClinicId == clinicId
                        && a.TimeSlot.StartUtc >= startUtc
                        && a.TimeSlot.StartUtc < endUtc
                        && AppointmentStatusExtensions.SlotHoldingStatuses.Contains(a.Status))
            .OrderBy(a => a.TimeSlot.StartUtc)
            .Select(a => new
            {
                a.Id,
                a.Reference,
                a.TimeSlot.StartUtc,
                a.Patient.FirstName,
                a.Patient.LastName,
                a.Patient.IdentityNumber,
                a.Patient.PhoneNumber,
                a.Status,
                a.ReasonForVisit
            })
            .ToListAsync(ct);

        var entries = rows.Select(r => new DayListEntryDto(
                r.Id,
                r.Reference,
                r.StartUtc.ToClinicTime(zone),
                $"{r.FirstName} {r.LastName}",
                ContractMapping.MaskIdentityNumber(r.IdentityNumber),
                r.PhoneNumber,
                r.Status.ToContract(),
                r.ReasonForVisit,
                CanRecordAttendance: r.Status == AppointmentStatus.Booked && r.StartUtc <= nowUtc))
            .ToList();

        return new DayListDto(clinic.Id, clinic.Name, date, totalSlots, entries.Count, entries);
    }

    public async Task RecordAttendanceAsync(int clinicId, int appointmentId, bool attended, CancellationToken ct)
    {
        // Filtering on clinic means staff at another clinic get a 404, same as a missing id.
        var appointment = await db.Appointments
            .Include(a => a.TimeSlot)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.TimeSlot.ClinicId == clinicId, ct)
            ?? throw new NotFoundException("Appointment");

        appointment.RecordAttendance(attended, time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Clinic {ClinicId} recorded {Status} for appointment {AppointmentId}",
            clinicId, appointment.Status, appointment.Id);
    }

    public async Task<GenerateSlotsResult> GenerateSlotsAsync(int clinicId, GenerateSlotsRequest request, CancellationToken ct)
    {
        var clinic = await db.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, ct)
            ?? throw new NotFoundException("Clinic");

        var template = new SlotTemplate(
            request.From, request.To, request.DayStart, request.DayEnd, request.SlotMinutes, request.Days.ToHashSet());
        var windows = SlotGenerator.Generate(template, clinic.TimeZone);
        if (windows.Count == 0) return new GenerateSlotsResult(0, 0);

        var firstUtc = windows[0].StartUtc;
        var lastUtc = windows[^1].StartUtc;
        var existing = (await db.TimeSlots
                .Where(s => s.ClinicId == clinicId && s.StartUtc >= firstUtc && s.StartUtc <= lastUtc)
                .Select(s => s.StartUtc)
                .ToListAsync(ct))
            .ToHashSet();

        // Overlap with existing slots of a different length isn't handled. Re-running with a new
        // slot length on days that already have slots would produce overlapping slots. Fine for
        // now: one slot length per clinic is the expected setup. Worth a rule later.
        var toCreate = windows
            .Where(w => !existing.Contains(w.StartUtc))
            .Select(w => new TimeSlot(clinicId, w.StartUtc, w.EndUtc))
            .ToList();

        db.TimeSlots.AddRange(toCreate);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation(TimeSlotConfiguration.UniqueClinicStartIndex))
        {
            throw new BookingRuleException("Someone else is generating slots for these days. Try again in a moment.");
        }

        logger.LogInformation("Clinic {ClinicId} generated {Count} slots from {From} to {To}",
            clinicId, toCreate.Count, request.From, request.To);

        return new GenerateSlotsResult(toCreate.Count, windows.Count - toCreate.Count);
    }
}
