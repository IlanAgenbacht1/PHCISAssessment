using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.Api.Features.Notifications;

/// <summary>
/// Seam for SMS/email (Twilio, SendGrid, or whatever the department already uses). Only a logging
/// implementation exists for now.
/// </summary>
public interface INotificationService
{
    Task SendBookingConfirmationAsync(int patientId, AppointmentDto appointment, CancellationToken ct);
    Task SendCancellationAsync(int patientId, AppointmentDto appointment, CancellationToken ct);
}

internal sealed class LoggingNotificationService(ILogger<LoggingNotificationService> logger) : INotificationService
{
    // Patient id and booking reference only. Names, ID numbers and phone numbers stay out of logs (POPIA).
    public Task SendBookingConfirmationAsync(int patientId, AppointmentDto appointment, CancellationToken ct)
    {
        logger.LogInformation(
            "Would send booking confirmation {Reference} for {Start} at clinic {ClinicId} to patient {PatientId}",
            appointment.Reference, appointment.Start, appointment.ClinicId, patientId);
        return Task.CompletedTask;
    }

    public Task SendCancellationAsync(int patientId, AppointmentDto appointment, CancellationToken ct)
    {
        logger.LogInformation(
            "Would send cancellation notice {Reference} to patient {PatientId}", appointment.Reference, patientId);
        return Task.CompletedTask;
    }
}
