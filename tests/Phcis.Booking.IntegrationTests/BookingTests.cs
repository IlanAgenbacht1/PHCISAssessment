using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Phcis.Booking.Shared;
using Phcis.Booking.Shared.Contracts;
using static Phcis.Booking.IntegrationTests.TestTimes;

namespace Phcis.Booking.IntegrationTests;

[Collection(ApiCollection.Name)]
public class BookingTests(BookingApiFactory api)
{
    [Fact]
    public async Task Concurrent_bookings_for_the_same_slot_only_one_wins()
    {
        var clinicId = await api.CreateClinicAsync();
        var slotId = (await api.CreateSlotsAsync(clinicId, FutureMorningUtc(3)))[0];
        var patients = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => api.CreatePatientClientAsync()));

        // Fire all ten at once. There's deliberately no "is it free?" check in code, so this is
        // entirely down to the partial unique index.
        var responses = await Task.WhenAll(patients.Select(p => p.BookAsync(slotId)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(9, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var anonymous = api.CreateAnonymousClient();
        var stillAvailable = await anonymous.GetOkAsync<List<TimeSlotDto>>(ApiRoutes.ClinicSlots(clinicId, ClinicDate(FutureMorningUtc(3))));
        Assert.DoesNotContain(stillAvailable, s => s.Id == slotId);
    }

    [Fact]
    public async Task Taken_slot_returns_problem_details_the_client_can_show()
    {
        var clinicId = await api.CreateClinicAsync();
        var slotId = (await api.CreateSlotsAsync(clinicId, FutureMorningUtc(4)))[0];
        await (await api.CreatePatientClientAsync()).BookOkAsync(slotId);

        var response = await (await api.CreatePatientClientAsync()).BookAsync(slotId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("just been taken", problem!.Detail);
    }

    [Fact]
    public async Task Cancelled_slot_can_be_booked_again()
    {
        var clinicId = await api.CreateClinicAsync();
        var slotId = (await api.CreateSlotsAsync(clinicId, FutureMorningUtc(5)))[0];
        var first = await api.CreatePatientClientAsync();
        var booking = await first.BookOkAsync(slotId);

        (await first.CancelAsync(booking.Reference)).EnsureSuccessStatusCode();
        var second = await (await api.CreatePatientClientAsync()).BookAsync(slotId);

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task Reschedule_to_a_taken_slot_keeps_the_original_booking()
    {
        var clinicId = await api.CreateClinicAsync();
        var slots = await api.CreateSlotsAsync(clinicId, FutureMorningUtc(6), FutureMorningUtc(6).AddHours(1));
        var patient = await api.CreatePatientClientAsync();
        var original = await patient.BookOkAsync(slots[0]);
        await (await api.CreatePatientClientAsync()).BookOkAsync(slots[1]);

        var response = await patient.RescheduleAsync(original.Reference, slots[1]);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var unchanged = await patient.GetOkAsync<AppointmentDto>(ApiRoutes.Appointment(original.Reference));
        Assert.Equal(AppointmentStatus.Booked, unchanged.Status);
    }

    [Fact]
    public async Task Reschedule_moves_the_booking_and_frees_the_old_slot()
    {
        var clinicId = await api.CreateClinicAsync();
        var start = FutureMorningUtc(7);
        var slots = await api.CreateSlotsAsync(clinicId, start, start.AddHours(1));
        var patient = await api.CreatePatientClientAsync();
        var original = await patient.BookOkAsync(slots[0]);

        var response = await patient.RescheduleAsync(original.Reference, slots[1]);

        response.EnsureSuccessStatusCode();
        var replacement = (await response.Content.ReadFromJsonAsync<AppointmentDto>(SharedJson.Options))!;
        Assert.NotEqual(original.Reference, replacement.Reference);

        var mine = await patient.GetOkAsync<List<AppointmentDto>>(ApiRoutes.Appointments);
        Assert.Equal(AppointmentStatus.Rescheduled, mine.Single(a => a.Reference == original.Reference).Status);

        var available = await patient.GetOkAsync<List<TimeSlotDto>>(ApiRoutes.ClinicSlots(clinicId, ClinicDate(start)));
        Assert.Equal([slots[0]], available.Select(s => s.Id));
    }

    [Fact]
    public async Task Patient_cannot_hold_overlapping_appointments_at_different_clinics()
    {
        var start = FutureMorningUtc(8);
        var slotA = (await api.CreateSlotsAsync(await api.CreateClinicAsync(), start))[0];
        var slotB = (await api.CreateSlotsAsync(await api.CreateClinicAsync(), start.AddMinutes(10)))[0];
        var patient = await api.CreatePatientClientAsync();
        await patient.BookOkAsync(slotA);

        var response = await patient.BookAsync(slotB);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Past_slots_are_neither_listed_nor_bookable()
    {
        var clinicId = await api.CreateClinicAsync();
        var past = DateTime.UtcNow.AddHours(-1);
        var slotId = (await api.CreateSlotsAsync(clinicId, past))[0];
        var patient = await api.CreatePatientClientAsync();

        var available = await patient.GetOkAsync<List<TimeSlotDto>>(ApiRoutes.ClinicSlots(clinicId, ClinicDate(past)));
        var response = await patient.BookAsync(slotId);

        Assert.Empty(available);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Available_slots_are_returned_in_clinic_time()
    {
        var clinicId = await api.CreateClinicAsync();
        var start = FutureMorningUtc(9); // 07:00 UTC
        await api.CreateSlotsAsync(clinicId, start);

        var slot = (await api.CreateAnonymousClient().GetOkAsync<List<TimeSlotDto>>(ApiRoutes.ClinicSlots(clinicId, ClinicDate(start)))).Single();

        Assert.Equal(TimeSpan.FromHours(2), slot.Start.Offset);
        Assert.Equal(9, slot.Start.Hour);
    }
}
