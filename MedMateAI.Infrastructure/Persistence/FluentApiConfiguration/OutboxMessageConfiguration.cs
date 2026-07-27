using MedMateAI.Domain.Entities;
using MedMateAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessage");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("OutboxMessageId").ValueGeneratedOnAdd();
        builder.Property(x => x.EventType).HasMaxLength(150).IsRequired();
        builder.Property(x => x.AggregateType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).HasDefaultValue(OutboxMessageStatus.Pending).IsRequired();
        builder.Property(x => x.AttemptCount).HasDefaultValue(0);
        builder.Property(x => x.LastError).HasMaxLength(4000);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
        builder.HasIndex(x => new { x.AggregateType, x.AggregateId });
    }
}
