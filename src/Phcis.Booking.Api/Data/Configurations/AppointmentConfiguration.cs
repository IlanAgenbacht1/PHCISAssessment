using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phcis.Booking.Domain.Appointments;

namespace Phcis.Booking.Api.Data.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public const string UniqueActiveSlotIndex = "ux_appointments_active_slot";

    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.Property(a => a.Reference).HasMaxLength(16);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.ReasonForVisit).HasMaxLength(Appointment.MaxReasonLength);
        builder.Property(a => a.CancellationReason).HasMaxLength(Appointment.MaxReasonLength);

        builder.HasIndex(a => a.Reference).IsUnique();
        builder.HasIndex(a => new { a.PatientId, a.Status });

        // This is the double-booking guard. A slot can have any number of cancelled or rescheduled
        // appointments, but only one that still holds it. Two concurrent bookings both pass
        // any check done in code; only one of them gets past this index.
        var holding = string.Join(", ", AppointmentStatusExtensions.SlotHoldingStatuses.Select(s => $"'{s}'"));
        builder.HasIndex(a => a.TimeSlotId)
            .IsUnique()
            .HasFilter($"status IN ({holding})")
            .HasDatabaseName(UniqueActiveSlotIndex);

        // Postgres xmin as an optimistic concurrency token: stops a patient cancelling while
        // reception marks them as attended, with the last write silently winning.
        builder.Property<uint>("Version").IsRowVersion();

        // Appointments are history. Nothing upstream gets to cascade-delete them.
        builder.HasOne(a => a.Patient).WithMany().HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.TimeSlot).WithMany().HasForeignKey(a => a.TimeSlotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Appointment>().WithMany().HasForeignKey(a => a.RescheduledFromId).OnDelete(DeleteBehavior.Restrict);
    }
}
