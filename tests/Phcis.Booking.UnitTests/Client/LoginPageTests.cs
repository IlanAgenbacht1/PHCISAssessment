using Bunit;
using FluentValidation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Phcis.Booking.Client.Pages;
using Phcis.Booking.Client.Services;
using Phcis.Booking.Shared.Contracts;
using Phcis.Booking.Shared.Validation;

namespace Phcis.Booking.UnitTests.Client;

public class LoginPageTests : MudTestContext
{
    private readonly Mock<IAccountApi> _account = new();

    public LoginPageTests()
    {
        Services.AddSingleton(_account.Object);
        Services.AddSingleton<IValidator<LoginRequest>, LoginRequestValidator>();
        Services.AddSingleton(Mock.Of<IWebAssemblyHostEnvironment>(e => e.Environment == "Production"));
    }

    [Fact]
    public void Shared_validator_blocks_an_empty_form_before_any_request()
    {
        var cut = Render<Login>();

        cut.Find("form").Submit();

        Assert.NotEmpty(cut.FindAll(".mud-input-helper-text.mud-input-error"));
        _account.Verify(a => a.LoginAsync(It.IsAny<LoginRequest>()), Times.Never);
    }

    [Theory]
    [InlineData("staff", Roles.Staff, null)]
    [InlineData("appointments", Roles.Patient, null)]
    [InlineData("clinics/1/book", Roles.Patient, "clinics/1/book")]
    [InlineData("appointments", Roles.Patient, "https://evil.example/phish")]
    public void Sends_users_to_the_right_place_after_sign_in(string expectedPath, string role, string? returnUrl)
    {
        _account.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync(ApiResult<UserInfo>.Success(new UserInfo("a@test.local", "A", [role], null)));

        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(returnUrl is null ? "login" : $"login?returnUrl={Uri.EscapeDataString(returnUrl)}");

        var cut = Render<Login>();
        cut.Find("input[type=email]").Change("a@test.local");
        cut.Find("input[type=password]").Change("Password123");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(navigation.BaseUri + expectedPath, navigation.Uri));
    }
}
