using System.Security.Claims;
using Phcis.Booking.Api.Infrastructure;
using Phcis.Booking.Shared;
using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.Api.Features.Staff;

internal static class StaffEndpoints
{
    // The clinic always comes from the signed-in user's claim, never from the request,
    // so there's no way to ask for another clinic's data.
    public static void MapStaffEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Staff)
            .WithTags("Staff")
            .RequireAuthorization(Policies.Staff);

        group.MapGet("/day-list", async (DateOnly date, ClaimsPrincipal user, IClinicScheduleService schedule, CancellationToken ct) =>
            TypedResults.Ok(await schedule.GetDayListAsync(user.GetClinicId(), date, ct)));

        group.MapPost("/appointments/{appointmentId:int}/attendance",
            async (int appointmentId, RecordAttendanceRequest request, ClaimsPrincipal user, IClinicScheduleService schedule, CancellationToken ct) =>
            {
                await schedule.RecordAttendanceAsync(user.GetClinicId(), appointmentId, request.Attended, ct);
                return TypedResults.NoContent();
            });

        group.MapPost("/slots", async (GenerateSlotsRequest request, ClaimsPrincipal user, IClinicScheduleService schedule, CancellationToken ct) =>
                TypedResults.Ok(await schedule.GenerateSlotsAsync(user.GetClinicId(), request, ct)))
            .Validate<GenerateSlotsRequest>();
    }
}
