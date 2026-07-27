using MedMateAI.Domain.Enums;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(x => x.TimeZoneId)
            .HasMaxLength(100)
            .HasDefaultValue("Asia/Ho_Chi_Minh")
            .IsRequired();
        builder.Property(x => x.DisplayName)
            .HasMaxLength(256);

        builder.Property(x => x.Address)
            .HasMaxLength(512);

        builder.Property(x => x.Status)
            .HasConversion<int>();
            

        builder.Property(x => x.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(x => x.IsFirstLogin)
            .HasDefaultValue(false);

             builder.Property(x => x.DeletedAt)
            .HasColumnType("timestamp with time zone");
      
        builder.Property(x => x.IsProfileCompleted)
            .HasDefaultValue(false);
    }
}

