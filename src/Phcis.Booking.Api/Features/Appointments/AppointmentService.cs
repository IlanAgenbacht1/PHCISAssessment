using Microsoft.EntityFrameworkCore;
using Phcis.Booking.Api.Data;
using Phcis.Booking.Api.Data.Configurations;
using Phcis.Booking.Api.Features.Notifications;
using Phcis.Booking.Api.Infrastructure;
using Phcis.Booking.Domain;
using Phcis.Booking.Domain.Appointments;
using Phcis.Booking.Domain.Scheduling;
using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.Api.Features.Appointments;

/// <summary>Patient-facing booking operations. Every method is scoped to the calling patient.</summary>
public interface IAppointmentService
{
    Task<AppointmentDto> BookAsync(int patientId, BookAppointmentRequest request, CancellationToken ct);
    Task<IReadOnlyList<AppointmentDto>> GetForPatientAsync(int patientId, CancellationToken ct);
    Task<AppointmentDto> GetAsync(int patientId, string reference, CancellationToken ct);
    Task<AppointmentDto> CancelAsync(int patientId, string reference, CancelAppointmentRequest request, CancellationToken ct);
    Task<AppointmentDto> RescheduleAsync(int patientId, string reference, RescheduleAppointmentRequest request, CancellationToken ct);
}

internal sealed class AppointmentService(
    BookingDbContext db,
    TimeProvider time,
    INotificationService notifications,
    ILogger<AppointmentService> logger) : IAppointmentService
{
    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public async Task<AppointmentDto> BookAsync(int patientId, BookAppointmentRequest request, CancellationToken ct)
    {
        var slot = await LoadSlotAsync(request.TimeSlotId, ct);
        await EnsureNoOverlapAsync(patientId, slot, excludingAppointmentId: null, ct);

        var appointment = Appointment.Book(patientId, slot, request.ReasonForVisit, UtcNow);
        db.Appointments.Add(appointment);
        await SaveBookingAsync(ct);

        logger.LogInformation("Patient {PatientId} booked slot {SlotId} as {Reference}", patientId, slot.Id, appointment.Reference);

        var dto = appointment.ToDto(UtcNow);
        await NotifySafelyAsync(() => notifications.SendBookingConfirmationAsync(patientId, dto, ct));
        return dto;
    }

    public async Task<IReadOnlyList<AppointmentDto>> GetForPatientAsync(int patientId, CancellationToken ct)
    {
        var appointments = await db.Appointments
            .AsNoTracking()
            .Include(a => a.TimeSlot).ThenInclude(s => s.Clinic)
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.TimeSlot.StartUtc)
            .ToListAsync(ct);

        return appointments.Select(a => a.ToDto(UtcNow)).ToList();
    }

    public async Task<AppointmentDto> GetAsync(int patientId, string reference, CancellationToken ct) =>
        (await LoadOwnAppointmentAsync(patientId, reference, ct)).ToDto(UtcNow);

    public async Task<AppointmentDto> CancelAsync(int patientId, string reference, CancelAppointmentRequest request, CancellationToken ct)
    {
        var appointment = await LoadOwnAppointmentAsync(patientId, reference, ct);

        appointment.Cancel(request.Reason, UtcNow);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Patient {PatientId} cancelled {Reference}", patientId, appointment.Reference);

        var dto = appointment.ToDto(UtcNow);
        await NotifySafelyAsync(() => notifications.SendCancellationAsync(patientId, dto, ct));
        return dto;
    }

    public async Task<AppointmentDto> RescheduleAsync(int patientId, string reference, RescheduleAppointmentRequest request, CancellationToken ct)
    {
        var original = await LoadOwnAppointmentAsync(patientId, reference, ct);
        var newSlot = await LoadSlotAsync(request.NewTimeSlotId, ct);
        await EnsureNoOverlapAsync(patientId, newSlot, excludingAppointmentId: original.Id, ct);

        var replacement = original.RescheduleTo(newSlot, UtcNow);
        db.Appointments.Add(replacement);

        // One SaveChanges = one transaction. If the new slot was taken in the meantime the index
        // rejects the insert, the update to the original rolls back with it, and the patient keeps
        // their original booking.
        await SaveBookingAsync(ct);

        logger.LogInformation("Patient {PatientId} moved {From} to {To}", patientId, original.Reference, replacement.Reference);

        var dto = replacement.ToDto(UtcNow);
        await NotifySafelyAsync(() => notifications.SendBookingConfirmationAsync(patientId, dto, ct));
        return dto;
    }

    private async Task<TimeSlot> LoadSlotAsync(int slotId, CancellationToken ct) =>
        await db.TimeSlots.Include(s => s.Clinic).FirstOrDefaultAsync(s => s.Id == slotId, ct)
        ?? throw new NotFoundException("Time slot");

    private async Task<Appointment> LoadOwnAppointmentAsync(int patientId, string reference, CancellationToken ct) =>
        await db.Appointments
            .Include(a => a.TimeSlot).ThenInclude(s => s.Clinic)
            .FirstOrDefaultAsync(a => a.Reference == reference && a.PatientId == patientId, ct)
        ?? throw new NotFoundException("Appointment");

    /// <summary>
    /// BR-03. Unlike the one-booking-per-slot rule this is only checked in code: two simultaneous
    /// bookings by the same patient at different clinics could both get through. A Postgres
    /// exclusion constraint over a tstzrange would close that gap; see the decisions log.
    /// </summary>
    private async Task EnsureNoOverlapAsync(int patientId, TimeSlot slot, int? excludingAppointmentId, CancellationToken ct)
    {
        var overlaps = await db.Appointments.AnyAsync(a =>
            a.PatientId == patientId
            && a.Id != excludingAppointmentId
            && AppointmentStatusExtensions.SlotHoldingStatuses.Contains(a.Status)
            && a.TimeSlot.StartUtc < slot.EndUtc
            && slot.StartUtc < a.TimeSlot.EndUtc, ct);

        if (overlaps)
            throw new BookingRuleException("You already have an appointment at that time.");
    }

    private async Task SaveBookingAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation(AppointmentConfiguration.UniqueActiveSlotIndex))
        {
            throw new SlotUnavailableException();
        }
    }

    // The booking is already saved at this point. A failed SMS shouldn't turn it into an error
    // response that makes the patient book again. In production this would go through an outbox.
    private async Task NotifySafelyAsync(Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Notification failed");
        }
    }
}
