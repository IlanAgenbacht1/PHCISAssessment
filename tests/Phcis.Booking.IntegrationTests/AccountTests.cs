using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Phcis.Booking.Shared;
using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AccountTests(BookingApiFactory api)
{
    private static RegisterPatientRequest SaIdRegistration(string idNumber, DateOnly dob) => new()
    {
        FirstName = "Thandi",
        LastName = "Mokoena",
        IdentityType = IdentityDocumentType.SaIdNumber,
        IdentityNumber = idNumber,
        DateOfBirth = dob,
        PhoneNumber = "0821234567",
        Email = $"{Guid.NewGuid():N}@test.local",
        Password = "Password123"
    };

    [Fact]
    public async Task Register_signs_the_patient_in()
    {
        var client = api.CreateAnonymousClient();

        var register = await client.PostAsJsonAsync($"/{ApiRoutes.Account}/register",
            SaIdRegistration("8001015009087", new DateOnly(1980, 1, 1)), SharedJson.Options);
        var me = await client.GetFromJsonAsync<UserInfo>($"/{ApiRoutes.Account}/me", SharedJson.Options);

        register.EnsureSuccessStatusCode();
        Assert.Equal("Thandi Mokoena", me!.DisplayName);
        Assert.Contains(Roles.Patient, me.Roles);
    }

    [Fact]
    public async Task Register_runs_the_shared_validator_on_the_server()
    {
        var response = await api.CreateAnonymousClient().PostAsJsonAsync($"/{ApiRoutes.Account}/register",
            SaIdRegistration("8001015009088", new DateOnly(1980, 1, 1)), SharedJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(nameof(RegisterPatientRequest.IdentityNumber), problem!.Errors.Keys);
    }

    [Fact]
    public async Task Same_id_number_cannot_register_twice()
    {
        var first = await api.CreateAnonymousClient().PostAsJsonAsync($"/{ApiRoutes.Account}/register",
            SaIdRegistration("9208235812088", new DateOnly(1992, 8, 23)), SharedJson.Options);
        var second = await api.CreateAnonymousClient().PostAsJsonAsync($"/{ApiRoutes.Account}/register",
            SaIdRegistration("9208235812088", new DateOnly(1992, 8, 23)), SharedJson.Options);

        first.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Me_returns_no_content_when_signed_out()
    {
        var response = await api.CreateAnonymousClient().GetAsync($"/{ApiRoutes.Account}/me");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_rejected_without_saying_which_part_was_wrong()
    {
        var response = await api.CreateAnonymousClient().PostAsJsonAsync($"/{ApiRoutes.Account}/login",
            new LoginRequest { Email = "nobody@test.local", Password = "Whatever123" }, SharedJson.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Email or password is incorrect.", problem!.Detail);
    }
}
