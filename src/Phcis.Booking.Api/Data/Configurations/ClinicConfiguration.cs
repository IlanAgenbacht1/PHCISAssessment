using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phcis.Booking.Domain.Clinics;

namespace Phcis.Booking.Api.Data.Configurations;

internal sealed class ClinicConfiguration : IEntityTypeConfiguration<Clinic>
{
    public void Configure(EntityTypeBuilder<Clinic> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(150);
        builder.Property(c => c.Address).HasMaxLength(300);
        builder.Property(c => c.PhoneNumber).HasMaxLength(20);
        builder.Property(c => c.TimeZoneId).HasMaxLength(64);
        builder.Ignore(c => c.TimeZone);
    }
}
