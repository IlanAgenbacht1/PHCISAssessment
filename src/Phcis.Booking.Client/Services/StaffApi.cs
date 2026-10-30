using Phcis.Booking.Client.Auth;
using Phcis.Booking.Shared;
using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.Client.Services;

public interface IStaffApi
{
    Task<ApiResult<DayListDto>> GetDayListAsync(DateOnly date, CancellationToken ct = default);
    Task<ApiResult> RecordAttendanceAsync(int appointmentId, bool attended);
    Task<ApiResult<GenerateSlotsResult>> GenerateSlotsAsync(GenerateSlotsRequest request);
}

public sealed class StaffApi(HttpClient http, CookieAuthenticationStateProvider auth) : ApiClientBase(http, auth), IStaffApi
{
    public Task<ApiResult<DayListDto>> GetDayListAsync(DateOnly date, CancellationToken ct = default) =>
        GetAsync<DayListDto>(ApiRoutes.StaffDayList(date), ct);

    public Task<ApiResult> RecordAttendanceAsync(int appointmentId, bool attended) =>
        PostAsync(ApiRoutes.StaffAttendance(appointmentId), new RecordAttendanceRequest { Attended = attended });

    public Task<ApiResult<GenerateSlotsResult>> GenerateSlotsAsync(GenerateSlotsRequest request) =>
        PostAsync<GenerateSlotsResult>(ApiRoutes.StaffSlots, request);
}
