using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class UserMedicationReminderTimeConfiguration : IEntityTypeConfiguration<UserMedicationReminderTime>
{
    public void Configure(EntityTypeBuilder<UserMedicationReminderTime> builder)
    {
        builder.ToTable("UserMedicationReminderTime");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("UserMedicationReminderTimeId").ValueGeneratedOnAdd();
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasIndex(x => x.UserMedicationId);
        builder.HasIndex(x => new { x.UserMedicationId, x.TimeOfDay }).IsUnique().HasFilter("\"IsDeleted\" = false AND \"IsActive\" = true");
        builder.HasOne(x => x.UserMedication).WithMany(x => x.ReminderTimes).HasForeignKey(x => x.UserMedicationId).OnDelete(DeleteBehavior.Cascade);
    }
}
