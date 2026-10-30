using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Phcis.Booking.Api.Features.Staff;
using Phcis.Booking.Api.Infrastructure;
using Phcis.Booking.Domain.Appointments;
using Phcis.Booking.Domain.Clinics;
using Phcis.Booking.Domain.Patients;
using Phcis.Booking.Shared.Contracts;
using DomainIdentityType = Phcis.Booking.Domain.Patients.IdentityDocumentType;

namespace Phcis.Booking.Api.Data;

/// <summary>
/// Demo data so there's something to click on. Safe to run on every startup: clinics, users and
/// appointments are only created once, and slots are topped up so there are always two weeks ahead.
/// All people, ID numbers and clinics are made up.
/// </summary>
internal sealed class DemoDataSeeder(
    BookingDbContext db,
    UserManager<IdentityUser> users,
    RoleManager<IdentityRole> roles,
    IClinicScheduleService schedule,
    TimeProvider time,
    ILogger<DemoDataSeeder> logger)
{
    public const string DemoPassword = "Clinic2026";
    private const string Zone = "Africa/Johannesburg";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        foreach (var role in new[] { Roles.Patient, Roles.Staff })
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));
        }

        if (!await db.Clinics.AnyAsync(ct))
        {
            db.Clinics.AddRange(
                new Clinic("Northside Community Day Centre", "14 Kloof Road, Northside", "0215550101", Zone),
                new Clinic("Harbour View Clinic", "2 Quay Street, Harbour View", "0215550102", Zone),
                new Clinic("Riverside Satellite Clinic", "Corner of Mill and River Roads, Riverside", "0215550103", Zone));
            await db.SaveChangesAsync(ct);
        }

        var clinics = await db.Clinics.OrderBy(c => c.Id).ToListAsync(ct);

        foreach (var clinic in clinics)
        {
            await TopUpSlotsAsync(clinic, ct);
        }

        await EnsureStaffAsync("reception.northside@phcis.local", "Northside Reception", clinics[0].Id);
        await EnsureStaffAsync("reception.harbour@phcis.local", "Harbour View Reception", clinics[1].Id);
        await EnsurePatientAsync("patient@phcis.local", "Thandi", "Mokoena", "9506150123088", new DateOnly(1995, 6, 15), "0821234567");

        if (!await db.Appointments.AnyAsync(ct))
        {
            await SeedTodaysListAsync(clinics[0], ct);
        }
    }

    private async Task TopUpSlotsAsync(Clinic clinic, CancellationToken ct)
    {
        var today = time.Today(clinic.TimeZone);
        var result = await schedule.GenerateSlotsAsync(clinic.Id, new GenerateSlotsRequest
        {
            From = today,
            To = today.AddDays(14)
        }, ct);

        if (result.Created > 0)
            logger.LogInformation("Seeded {Count} slots for clinic {ClinicId}", result.Created, clinic.Id);
    }

    private async Task EnsureStaffAsync(string email, string displayName, int clinicId)
    {
        if (await users.FindByNameAsync(email) is not null) return;

        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        await users.CreateAsync(user, DemoPassword);
        await users.AddToRoleAsync(user, Roles.Staff);
        await users.AddClaimsAsync(user,
        [
            new Claim(AppClaims.ClinicId, clinicId.ToString()),
            new Claim(AppClaims.DisplayName, displayName)
        ]);
    }

    private async Task EnsurePatientAsync(string email, string firstName, string lastName, string idNumber, DateOnly dob, string phone)
    {
        if (await users.FindByNameAsync(email) is not null) return;

        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        await users.CreateAsync(user, DemoPassword);
        await users.AddToRoleAsync(user, Roles.Patient);

        var patient = new Patient(user.Id, firstName, lastName, DomainIdentityType.SaIdNumber, idNumber, dob, phone, email);
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        await users.AddClaimsAsync(user,
        [
            new Claim(AppClaims.PatientId, patient.Id.ToString()),
            new Claim(AppClaims.DisplayName, patient.FullName)
        ]);
    }

    /// <summary>
    /// Walk-in style patients (no online account) booked into today's list, so the staff screen has
    /// something on it, including some appointments that have already started and can be marked.
    /// </summary>
    private async Task SeedTodaysListAsync(Clinic clinic, CancellationToken ct)
    {
        var walkIns = new[]
        {
            new Patient(null, "Sipho", "Dlamini", DomainIdentityType.SaIdNumber, "8704120245088", new(1987, 4, 12), "0731112222", ""),
            new Patient(null, "Anele", "Jacobs", DomainIdentityType.SaIdNumber, "9208235812088", new(1992, 8, 23), "0742223333", ""),
            new Patient(null, "Johannes", "van Wyk", DomainIdentityType.SaIdNumber, "6701050126082", new(1967, 1, 5), "0613334444", ""),
            new Patient(null, "Lerato", "Khumalo", DomainIdentityType.SaIdNumber, "0111306123089", new(2001, 11, 30), "0794445555", ""),
            new Patient(null, "Grace", "Mutasa", DomainIdentityType.Passport, "FN4471023", new(1990, 3, 14), "0825556666", "")
        };
        db.Patients.AddRange(walkIns);
        await db.SaveChangesAsync(ct);

        var (startUtc, endUtc) = ClinicTime.DayBoundsUtc(time.Today(clinic.TimeZone), clinic.TimeZone);
        var todaysSlots = await db.TimeSlots
            .Where(s => s.ClinicId == clinic.Id && s.StartUtc >= startUtc && s.StartUtc < endUtc)
            .OrderBy(s => s.StartUtc)
            .ToListAsync(ct);

        // Every third slot, so there are gaps to book into. "Booked yesterday" is the only way to
        // put a patient in a slot that has already started without bending the domain rules.
        var reasons = new[] { "Chronic medication collection", "Blood pressure check", null, "Child immunisation", "Follow-up visit" };
        var picked = todaysSlots.Where((_, i) => i % 3 == 0).Take(walkIns.Length).ToList();
        for (var i = 0; i < picked.Count; i++)
        {
            db.Appointments.Add(Appointment.Book(walkIns[i].Id, picked[i], reasons[i], picked[i].StartUtc.AddDays(-1)));
        }

        await db.SaveChangesAsync(ct);
    }
}
