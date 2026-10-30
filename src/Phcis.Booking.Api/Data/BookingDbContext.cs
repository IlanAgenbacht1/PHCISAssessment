using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Phcis.Booking.Domain.Appointments;
using Phcis.Booking.Domain.Clinics;
using Phcis.Booking.Domain.Patients;
using Phcis.Booking.Domain.Scheduling;
using AppRoles = Phcis.Booking.Shared.Contracts.Roles;

namespace Phcis.Booking.Api.Data;

public class BookingDbContext(DbContextOptions<BookingDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(BookingDbContext).Assembly);

        // Roles are reference data the app can't run without, so they ship with the schema rather
        // than with the demo seed. Fixed ids and stamps keep the migration stable.
        builder.Entity<IdentityRole>().HasData(
            new IdentityRole { Id = "role-patient", Name = AppRoles.Patient, NormalizedName = "PATIENT", ConcurrencyStamp = "patient-v1" },
            new IdentityRole { Id = "role-staff", Name = AppRoles.Staff, NormalizedName = "STAFF", ConcurrencyStamp = "staff-v1" });
    }
}
