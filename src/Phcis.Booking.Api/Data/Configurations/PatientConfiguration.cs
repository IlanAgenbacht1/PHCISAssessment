using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phcis.Booking.Domain.Patients;

namespace Phcis.Booking.Api.Data.Configurations;

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.Property(p => p.FirstName).HasMaxLength(100);
        builder.Property(p => p.LastName).HasMaxLength(100);
        builder.Property(p => p.IdentityType).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.IdentityNumber).HasMaxLength(20);
        builder.Property(p => p.PhoneNumber).HasMaxLength(20);
        builder.Property(p => p.Email).HasMaxLength(256);
        builder.Ignore(p => p.FullName);

        builder.HasIndex(p => new { p.IdentityType, p.IdentityNumber }).IsUnique();

        // Closing an online account must not take the clinical record with it.
        builder.HasOne<IdentityUser>()
            .WithOne()
            .HasForeignKey<Patient>(p => p.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
