using MedMateAI.Domain.Entities;
using MedMateAI.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class FollowUpReminderConfiguration : IEntityTypeConfiguration<FollowUpReminder>
{
    public void Configure(EntityTypeBuilder<FollowUpReminder> builder)
    {
        builder.ToTable("FollowUpReminder");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("ReminderId").ValueGeneratedOnAdd();

        builder.HasOne<ApplicationUser>()
            .WithMany(x => x.FollowUpReminders)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TreatmentLog)
            .WithMany(x => x.FollowUpReminders)
            .HasForeignKey(x => x.TreatmentLogId)
            .OnDelete(DeleteBehavior.Restrict);

    }
}
