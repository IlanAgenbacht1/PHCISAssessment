using Microsoft.AspNetCore.Http.HttpResults;
using Phcis.Booking.Shared;
using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.Api.Features.Clinics;

internal static class ClinicEndpoints
{
    // Anonymous on purpose: people should be able to see what's available before creating an account.
    public static void MapClinicEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Clinics).WithTags("Clinics");

        group.MapGet("/", async (IClinicQueries clinics, CancellationToken ct) =>
            TypedResults.Ok(await clinics.GetClinicsAsync(ct)));

        group.MapGet("/{clinicId:int}/slots", GetSlots);
    }

    private static async Task<Ok<IReadOnlyList<TimeSlotDto>>> GetSlots(
        int clinicId, DateOnly date, IClinicQueries clinics, CancellationToken ct) =>
        TypedResults.Ok(await clinics.GetAvailableSlotsAsync(clinicId, date, ct));
}
