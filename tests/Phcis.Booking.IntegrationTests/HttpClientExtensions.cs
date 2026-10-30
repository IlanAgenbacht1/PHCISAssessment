using System.Net.Http.Json;
using Phcis.Booking.Shared;
using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.IntegrationTests;

internal static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> BookAsync(this HttpClient client, int slotId) =>
        client.PostAsJsonAsync($"/{ApiRoutes.Appointments}", new BookAppointmentRequest { TimeSlotId = slotId }, SharedJson.Options);

    public static async Task<AppointmentDto> BookOkAsync(this HttpClient client, int slotId)
    {
        var response = await client.BookAsync(slotId);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AppointmentDto>(SharedJson.Options))!;
    }

    public static Task<HttpResponseMessage> CancelAsync(this HttpClient client, string reference, string reason = "Can't make it") =>
        client.PostAsJsonAsync($"/{ApiRoutes.CancelAppointment(reference)}", new CancelAppointmentRequest { Reason = reason }, SharedJson.Options);

    public static Task<HttpResponseMessage> RescheduleAsync(this HttpClient client, string reference, int newSlotId) =>
        client.PostAsJsonAsync($"/{ApiRoutes.RescheduleAppointment(reference)}", new RescheduleAppointmentRequest { NewTimeSlotId = newSlotId }, SharedJson.Options);

    public static async Task<T> GetOkAsync<T>(this HttpClient client, string url) =>
        (await client.GetFromJsonAsync<T>($"/{url}", SharedJson.Options))!;
}
