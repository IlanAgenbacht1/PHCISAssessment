using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phcis.Booking.Domain.Scheduling;

namespace Phcis.Booking.Api.Data.Configurations;

internal sealed class TimeSlotConfiguration : IEntityTypeConfiguration<TimeSlot>
{
    public const string UniqueClinicStartIndex = "ux_time_slots_clinic_start";

    public void Configure(EntityTypeBuilder<TimeSlot> builder)
    {
        // Doubles as the index for "slots at clinic X on day Y", the hottest query in the system.
        builder.HasIndex(s => new { s.ClinicId, s.StartUtc })
            .IsUnique()
            .HasDatabaseName(UniqueClinicStartIndex);

        builder.HasOne(s => s.Clinic)
            .WithMany()
            .HasForeignKey(s => s.ClinicId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint("ck_time_slots_end_after_start", "end_utc > start_utc"));
    }
}
