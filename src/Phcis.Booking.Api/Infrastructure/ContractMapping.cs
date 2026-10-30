using Phcis.Booking.Domain.Appointments;
using Contracts = Phcis.Booking.Shared.Contracts;
using Domain = Phcis.Booking.Domain;

namespace Phcis.Booking.Api.Infrastructure;

/// <summary>
/// Hand-written mapping. With four entities it's shorter than configuring a mapper, and a renamed
/// property breaks the build instead of turning into a silent null at runtime.
/// </summary>
internal static class ContractMapping
{
    public static Contracts.AppointmentStatus ToContract(this AppointmentStatus status) => status switch
    {
        AppointmentStatus.Booked => Contracts.AppointmentStatus.Booked,
        AppointmentStatus.Cancelled => Contracts.AppointmentStatus.Cancelled,
        AppointmentStatus.Rescheduled => Contracts.AppointmentStatus.Rescheduled,
        AppointmentStatus.Attended => Contracts.AppointmentStatus.Attended,
        AppointmentStatus.NoShow => Contracts.AppointmentStatus.NoShow,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static Domain.Patients.IdentityDocumentType ToDomain(this Contracts.IdentityDocumentType type) => type switch
    {
        Contracts.IdentityDocumentType.SaIdNumber => Domain.Patients.IdentityDocumentType.SaIdNumber,
        Contracts.IdentityDocumentType.Passport => Domain.Patients.IdentityDocumentType.Passport,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    /// <summary>Needs TimeSlot and TimeSlot.Clinic loaded.</summary>
    public static Contracts.AppointmentDto ToDto(this Appointment appointment, DateTime nowUtc)
    {
        var slot = appointment.TimeSlot;
        var zone = slot.Clinic.TimeZone;

        return new Contracts.AppointmentDto(
            appointment.Id,
            appointment.Reference,
            slot.ClinicId,
            slot.Clinic.Name,
            slot.Clinic.Address,
            slot.StartUtc.ToClinicTime(zone),
            slot.EndUtc.ToClinicTime(zone),
            appointment.Status.ToContract(),
            appointment.ReasonForVisit,
            appointment.CancellationReason,
            appointment.CanBeChanged(nowUtc));
    }

    /// <summary>Last four characters only. Enough for reception to match the patient's document.</summary>
    public static string MaskIdentityNumber(string identityNumber) =>
        identityNumber.Length <= 4 ? identityNumber : new string('*', identityNumber.Length - 4) + identityNumber[^4..];
}
