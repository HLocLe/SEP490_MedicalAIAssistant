using MedMateAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedMateAI.Infrastructure.Persistence.FluentAPiConfiguration;

public sealed class QuotaConfiguration : IEntityTypeConfiguration<Quota>
{
    private static readonly Guid ServiceCreditQuotaId = new("7c57cfd1-5bb6-4d4e-8959-9e87d240d481");
    private static readonly DateTime SeededAt = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Quota> builder)
    {
        builder.ToTable("Quota");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("QuotaId").ValueGeneratedOnAdd();
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Unit).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasData(new Quota
        {
            Id = ServiceCreditQuotaId,
            Code = "SERVICE_CREDIT",
            Name = "Service Credit",
            Description = "Shared credit for eligible MediMate AI services.",
            Unit = "credit",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = SeededAt
        });
    }
}
