using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Phcis.Booking.Api.Infrastructure;
using Phcis.Booking.Shared;
using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.Api.Features.Appointments;

internal static class AppointmentEndpoints
{
    public static void MapAppointmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Appointments)
            .WithTags("Appointments")
            .RequireAuthorization(Policies.Patient);

        group.MapGet("/", async (ClaimsPrincipal user, IAppointmentService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetForPatientAsync(user.GetPatientId(), ct)));

        group.MapGet("/{reference}", async (string reference, ClaimsPrincipal user, IAppointmentService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(user.GetPatientId(), reference, ct)));

        group.MapPost("/", Book).Validate<BookAppointmentRequest>();

        group.MapPost("/{reference}/cancel",
                async (string reference, CancelAppointmentRequest request, ClaimsPrincipal user, IAppointmentService service, CancellationToken ct) =>
                    TypedResults.Ok(await service.CancelAsync(user.GetPatientId(), reference, request, ct)))
            .Validate<CancelAppointmentRequest>();

        group.MapPost("/{reference}/reschedule",
                async (string reference, RescheduleAppointmentRequest request, ClaimsPrincipal user, IAppointmentService service, CancellationToken ct) =>
                    TypedResults.Ok(await service.RescheduleAsync(user.GetPatientId(), reference, request, ct)))
            .Validate<RescheduleAppointmentRequest>();
    }

    private static async Task<Created<AppointmentDto>> Book(
        BookAppointmentRequest request, ClaimsPrincipal user, IAppointmentService service, CancellationToken ct)
    {
        var appointment = await service.BookAsync(user.GetPatientId(), request, ct);
        return TypedResults.Created($"/{ApiRoutes.Appointment(appointment.Reference)}", appointment);
    }
}
