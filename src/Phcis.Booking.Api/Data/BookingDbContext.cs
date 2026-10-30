using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Phcis.Booking.Domain.Appointments;
using Phcis.Booking.Domain.Clinics;
using Phcis.Booking.Domain.Patients;
using Phcis.Booking.Domain.Scheduling;

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
    }
}
