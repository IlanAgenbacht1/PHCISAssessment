using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Phcis.Booking.Api.Data;
using Phcis.Booking.Api.Infrastructure;
using Phcis.Booking.Domain.Clinics;
using Phcis.Booking.Domain.Scheduling;
using Phcis.Booking.Shared;
using Phcis.Booking.Shared.Contracts;

namespace Phcis.Booking.IntegrationTests;

/// <summary>
/// Runs the real API against a real Postgres, in a throwaway database per test run. The EF in-memory
/// provider would be quicker but doesn't enforce unique indexes, which is the thing most worth testing.
///
/// Point PHCIS_TEST_DB at a server to use something other than localhost:5432 (CI uses a service container).
/// </summary>
public sealed class BookingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _connectionString = new NpgsqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("PHCIS_TEST_DB") ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres")
    {
        Database = $"phcis_test_{Guid.NewGuid():N}"
    }.ConnectionString;

    private int _sequence;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Booking", _connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Database:SeedDemoData", "false");
        builder.UseSetting("RateLimiting:AuthPermitsPerMinute", "100000");
    }

    public Task InitializeAsync()
    {
        _ = Server; // start the host, which runs the migrations
        return Task.CompletedTask;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_connectionString) { Database = "postgres" }.ConnectionString);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{new NpgsqlConnectionStringBuilder(_connectionString).Database}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }

    /// <summary>Non-development environments use Secure cookies, so the client must talk https.</summary>
    public HttpClient CreateAnonymousClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    public async Task<int> CreateClinicAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var clinic = new Clinic($"Test Clinic {Interlocked.Increment(ref _sequence)}", "1 Test Street", "0210000000", "Africa/Johannesburg");
        db.Clinics.Add(clinic);
        await db.SaveChangesAsync();
        return clinic.Id;
    }

    /// <summary>Adds 20-minute slots at the given UTC start times and returns their ids in the same order.</summary>
    public async Task<int[]> CreateSlotsAsync(int clinicId, params DateTime[] startsUtc)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var slots = startsUtc.Select(s => new TimeSlot(clinicId, s, s.AddMinutes(20))).ToList();
        db.TimeSlots.AddRange(slots);
        await db.SaveChangesAsync();
        return slots.Select(s => s.Id).ToArray();
    }

    /// <summary>Registers a new patient through the API and returns a client signed in as them.</summary>
    public async Task<HttpClient> CreatePatientClientAsync()
    {
        var n = Interlocked.Increment(ref _sequence);
        var client = CreateAnonymousClient();
        var response = await client.PostAsJsonAsync($"/{ApiRoutes.Account}/register", new RegisterPatientRequest
        {
            FirstName = "Test",
            LastName = $"Patient{n}",
            IdentityType = IdentityDocumentType.Passport,
            IdentityNumber = $"TP{Guid.NewGuid():N}"[..12],
            DateOfBirth = new DateOnly(1990, 1, 1),
            PhoneNumber = "0820000000",
            Email = $"patient{n}-{Guid.NewGuid():N}@test.local",
            Password = "Password123"
        }, SharedJson.Options);
        response.EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>Staff accounts aren't self-service, so create one directly and sign in through the API.</summary>
    public async Task<HttpClient> CreateStaffClientAsync(int clinicId)
    {
        var email = $"staff{Interlocked.Increment(ref _sequence)}@test.local";
        await using (var scope = Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = email, Email = email };
            (await users.CreateAsync(user, "Password123")).ThrowIfFailed();
            (await users.AddToRoleAsync(user, Roles.Staff)).ThrowIfFailed();
            (await users.AddClaimAsync(user, new Claim(AppClaims.ClinicId, clinicId.ToString()))).ThrowIfFailed();
        }

        var client = CreateAnonymousClient();
        var response = await client.PostAsJsonAsync($"/{ApiRoutes.Account}/login",
            new LoginRequest { Email = email, Password = "Password123" }, SharedJson.Options);
        response.EnsureSuccessStatusCode();
        return client;
    }
}

internal static class IdentityResultExtensions
{
    public static void ThrowIfFailed(this IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<BookingApiFactory>
{
    public const string Name = "api";
}
