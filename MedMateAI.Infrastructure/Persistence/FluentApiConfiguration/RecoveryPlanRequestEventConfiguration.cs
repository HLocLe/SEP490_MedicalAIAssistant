using MedMateAI.Domain.Entities;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class RecoveryPlanRequestEventConfiguration : IEntityTypeConfiguration<RecoveryPlanRequestEvent>
{
    public void Configure(EntityTypeBuilder<RecoveryPlanRequestEvent> builder)
    {
        builder.ToTable("RecoveryPlanRequestEvent");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("RecoveryPlanRequestEventId").ValueGeneratedOnAdd();
        builder.Property(x => x.EventType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => new { x.RecoveryPlanRequestId, x.CreatedAt });
        builder.HasIndex(x => x.EventType);
        builder.HasOne(x => x.RecoveryPlanRequest).WithMany(x => x.Events).HasForeignKey(x => x.RecoveryPlanRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(x => x.ActorDoctorId).OnDelete(DeleteBehavior.SetNull);
    }
}
