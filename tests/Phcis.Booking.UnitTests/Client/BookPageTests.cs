using System.Net;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using Phcis.Booking.Client.Pages;
using Phcis.Booking.Client.Services;
using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.UnitTests.Client;

public class BookPageTests : MudTestContext
{
    private static readonly ClinicDto Clinic = new(1, "Northside CDC", "14 Kloof Road", "0215550101");

    private readonly Mock<IBookingApi> _api = new();
    private readonly Mock<ISnackbar> _snackbar = new();

    public BookPageTests()
    {
        AddAuthorization().SetAuthorized("patient@test.local").SetRoles(Roles.Patient);
        Services.AddSingleton(_api.Object);
        Services.AddSingleton(_snackbar.Object);

        _api.Setup(a => a.GetClinicsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<List<ClinicDto>>.Success([Clinic]));
    }

    [Fact]
    public void Successful_booking_goes_to_the_confirmation()
    {
        _api.Setup(a => a.GetAvailableSlotsAsync(1, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<List<TimeSlotDto>>.Success([Slot(10, 9)]));
        _api.Setup(a => a.BookAsync(It.Is<BookAppointmentRequest>(r => r.TimeSlotId == 10)))
            .ReturnsAsync(ApiResult<AppointmentDto>.Success(Booked("PHC-ABC234")));

        var cut = Render<Book>(p => p.Add(x => x.ClinicId, 1));
        cut.WaitForElement("[data-slot-id='10']").Click();
        ConfirmButton(cut).Click();

        var navigation = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => Assert.EndsWith("appointments/PHC-ABC234?confirmed=true", navigation.Uri));
    }

    [Fact]
    public void Slot_taken_by_someone_else_refreshes_the_list_and_explains_why()
    {
        _api.SetupSequence(a => a.GetAvailableSlotsAsync(1, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<List<TimeSlotDto>>.Success([Slot(10, 9), Slot(11, 10)]))
            .ReturnsAsync(ApiResult<List<TimeSlotDto>>.Success([Slot(11, 10)]));
        _api.Setup(a => a.BookAsync(It.IsAny<BookAppointmentRequest>()))
            .ReturnsAsync(ApiResult<AppointmentDto>.Failure(
                ApiError.From(HttpStatusCode.Conflict, "Sorry, that slot has just been taken.", null)));

        var cut = Render<Book>(p => p.Add(x => x.ClinicId, 1));
        cut.WaitForElement("[data-slot-id='10']").Click();
        ConfirmButton(cut).Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-slot-id='10']")));
        Assert.Single(cut.FindAll("[data-slot-id='11']"));
        Assert.Contains("Pick a time", ConfirmButton(cut).TextContent);
        _snackbar.Verify(s => s.Add("Sorry, that slot has just been taken.", Severity.Warning, null, null), Times.Once);
    }

    [Fact]
    public void Signed_out_visitors_can_browse_but_are_asked_to_sign_in()
    {
        AddAuthorization().SetNotAuthorized();
        _api.Setup(a => a.GetAvailableSlotsAsync(1, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<List<TimeSlotDto>>.Success([Slot(10, 9)]));

        var cut = Render<Book>(p => p.Add(x => x.ClinicId, 1));

        cut.WaitForElement("[data-slot-id='10']");
        var signIn = cut.Find("a[href^='login?returnUrl=']");
        Assert.Contains("Sign in to book", signIn.TextContent);
        _api.Verify(a => a.BookAsync(It.IsAny<BookAppointmentRequest>()), Times.Never);
    }

    private static AngleSharp.Dom.IElement ConfirmButton(IRenderedComponent<Book> cut) =>
        cut.FindAll("button").Single(b => b.TextContent.Contains("Confirm") || b.TextContent.Contains("Pick a time"));

    private static AppointmentDto Booked(string reference) =>
        new(1, reference, 1, Clinic.Name, Clinic.Address, Slot(10, 9).Start, Slot(10, 9).End,
            AppointmentStatus.Booked, null, null, CanBeChanged: true);
}
