using MedMateAI.Domain.Entities;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class UserPushDeviceConfiguration :
    IEntityTypeConfiguration<UserPushDevice>
{
    public void Configure(EntityTypeBuilder<UserPushDevice> builder)
    {
        builder.ToTable("UserPushDevice");

        builder.HasKey(device => device.Id);
        builder.Property(device => device.Id)
            .HasColumnName("UserPushDeviceId")
            .ValueGeneratedOnAdd();
        builder.Property(device => device.InstallationId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(device => device.ExpoPushToken)
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(device => device.TokenVersion)
            .HasDefaultValue(1)
            .IsRequired();
        builder.Property(device => device.Platform)
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(device => device.AppVersion).HasMaxLength(64);
        builder.Property(device => device.IsActive)
            .HasDefaultValue(true)
            .IsRequired();
        builder.Property(device => device.LastSeenAt).IsRequired();

        builder.HasIndex(device => new { device.UserId, device.IsActive });
        builder.HasIndex(device => device.ExpoPushToken)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"IsActive\" = true");
        builder.HasIndex(device => new { device.UserId, device.InstallationId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasOne<ApplicationUser>()
            .WithMany(user => user.PushDevices)
            .HasForeignKey(device => device.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
