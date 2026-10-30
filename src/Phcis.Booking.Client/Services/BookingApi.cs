using Phcis.Booking.Client.Auth;
using Phcis.Booking.Shared;
using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.Client.Services;

public interface IBookingApi
{
    Task<ApiResult<List<ClinicDto>>> GetClinicsAsync(CancellationToken ct = default);
    Task<ApiResult<List<TimeSlotDto>>> GetAvailableSlotsAsync(int clinicId, DateOnly date, CancellationToken ct = default);
    Task<ApiResult<List<AppointmentDto>>> GetMyAppointmentsAsync(CancellationToken ct = default);
    Task<ApiResult<AppointmentDto>> GetAppointmentAsync(string reference, CancellationToken ct = default);
    Task<ApiResult<AppointmentDto>> BookAsync(BookAppointmentRequest request);
    Task<ApiResult<AppointmentDto>> CancelAsync(string reference, CancelAppointmentRequest request);
    Task<ApiResult<AppointmentDto>> RescheduleAsync(string reference, RescheduleAppointmentRequest request);
}

public sealed class BookingApi(HttpClient http, CookieAuthenticationStateProvider auth) : ApiClientBase(http, auth), IBookingApi
{
    public Task<ApiResult<List<ClinicDto>>> GetClinicsAsync(CancellationToken ct = default) =>
        GetAsync<List<ClinicDto>>(ApiRoutes.Clinics, ct);

    public Task<ApiResult<List<TimeSlotDto>>> GetAvailableSlotsAsync(int clinicId, DateOnly date, CancellationToken ct = default) =>
        GetAsync<List<TimeSlotDto>>(ApiRoutes.ClinicSlots(clinicId, date), ct);

    public Task<ApiResult<List<AppointmentDto>>> GetMyAppointmentsAsync(CancellationToken ct = default) =>
        GetAsync<List<AppointmentDto>>(ApiRoutes.Appointments, ct);

    public Task<ApiResult<AppointmentDto>> GetAppointmentAsync(string reference, CancellationToken ct = default) =>
        GetAsync<AppointmentDto>(ApiRoutes.Appointment(reference), ct);

    public Task<ApiResult<AppointmentDto>> BookAsync(BookAppointmentRequest request) =>
        PostAsync<AppointmentDto>(ApiRoutes.Appointments, request);

    public Task<ApiResult<AppointmentDto>> CancelAsync(string reference, CancelAppointmentRequest request) =>
        PostAsync<AppointmentDto>(ApiRoutes.CancelAppointment(reference), request);

    public Task<ApiResult<AppointmentDto>> RescheduleAsync(string reference, RescheduleAppointmentRequest request) =>
        PostAsync<AppointmentDto>(ApiRoutes.RescheduleAppointment(reference), request);
}
