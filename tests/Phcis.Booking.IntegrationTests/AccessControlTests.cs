using System.Net;
using System.Net.Http.Json;
using Phcis.Booking.Shared;
using Phcis.Booking.Shared.Contracts;
using static Phcis.Booking.IntegrationTests.TestTimes;

namespace Phcis.Booking.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AccessControlTests(BookingApiFactory api)
{
    [Fact]
    public async Task Anonymous_users_can_browse_but_not_book()
    {
        var clinicId = await api.CreateClinicAsync();
        var slotId = (await api.CreateSlotsAsync(clinicId, FutureMorningUtc(10)))[0];
        var anonymous = api.CreateAnonymousClient();

        var browse = await anonymous.GetAsync($"/{ApiRoutes.Clinics}");
        var book = await anonymous.BookAsync(slotId);

        Assert.Equal(HttpStatusCode.OK, browse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, book.StatusCode);
    }

    [Fact]
    public async Task Another_patients_booking_reference_looks_like_it_does_not_exist()
    {
        var clinicId = await api.CreateClinicAsync();
        var slotId = (await api.CreateSlotsAsync(clinicId, FutureMorningUtc(11)))[0];
        var owner = await api.CreatePatientClientAsync();
        var booking = await owner.BookOkAsync(slotId);
        var intruder = await api.CreatePatientClientAsync();

        var view = await intruder.GetAsync($"/{ApiRoutes.Appointment(booking.Reference)}");
        var cancel = await intruder.CancelAsync(booking.Reference);

        Assert.Equal(HttpStatusCode.NotFound, view.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        var stillBooked = await owner.GetOkAsync<AppointmentDto>(ApiRoutes.Appointment(booking.Reference));
        Assert.Equal(AppointmentStatus.Booked, stillBooked.Status);
    }

    [Fact]
    public async Task Patients_cannot_use_staff_endpoints_and_staff_cannot_book()
    {
        var clinicId = await api.CreateClinicAsync();
        var slotId = (await api.CreateSlotsAsync(clinicId, FutureMorningUtc(12)))[0];
        var patient = await api.CreatePatientClientAsync();
        var staff = await api.CreateStaffClientAsync(clinicId);

        var patientToStaff = await patient.GetAsync($"/{ApiRoutes.StaffDayList(DateOnly.FromDateTime(DateTime.UtcNow))}");
        var staffBooking = await staff.BookAsync(slotId);

        Assert.Equal(HttpStatusCode.Forbidden, patientToStaff.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, staffBooking.StatusCode);
    }

    [Fact]
    public async Task Staff_only_see_and_update_their_own_clinic()
    {
        var start = FutureMorningUtc(13);
        var myClinic = await api.CreateClinicAsync();
        var otherClinic = await api.CreateClinicAsync();
        await api.CreateSlotsAsync(myClinic, start);
        var otherSlot = (await api.CreateSlotsAsync(otherClinic, start))[0];
        var otherBooking = await (await api.CreatePatientClientAsync()).BookOkAsync(otherSlot);
        var staff = await api.CreateStaffClientAsync(myClinic);

        var dayList = await staff.GetOkAsync<DayListDto>(ApiRoutes.StaffDayList(ClinicDate(start)));
        var attendance = await staff.PostAsJsonAsync($"/{ApiRoutes.StaffAttendance(otherBooking.Id)}",
            new RecordAttendanceRequest { Attended = true }, SharedJson.Options);

        Assert.Equal(myClinic, dayList.ClinicId);
        Assert.Empty(dayList.Appointments);
        Assert.Equal(HttpStatusCode.NotFound, attendance.StatusCode);
    }
}
