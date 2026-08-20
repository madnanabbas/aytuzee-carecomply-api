using CareComply.Application.Auditing;
using CareComply.Infrastructure.Auditing;
using CareComply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareComply.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> entity)
    {
        entity.ToTable("AuditLogs", table =>
        {
            table.HasCheckConstraint(
                "CK_AuditLogs_Outcome",
                "[Outcome] IN ('Succeeded', 'Failed')");
            table.HasCheckConstraint(
                "CK_AuditLogs_MetadataJson",
                "[MetadataJson] IS NULL OR ISJSON([MetadataJson]) = 1");
        });

        entity.HasKey(auditLog => auditLog.Id)
            .HasName("PK_AuditLogs");
        entity.Property(auditLog => auditLog.Id)
            .ValueGeneratedNever();
        entity.Property(auditLog => auditLog.OccurredAtUtc)
            .IsRequired();
        entity.Property(auditLog => auditLog.Action)
            .HasMaxLength(128)
            .IsRequired();
        entity.Property(auditLog => auditLog.ResourceType)
            .HasMaxLength(64)
            .IsRequired();
        entity.Property(auditLog => auditLog.ResourceId)
            .HasMaxLength(128)
            .IsRequired();
        entity.Property(auditLog => auditLog.Outcome)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsRequired();
        entity.Property(auditLog => auditLog.TraceId)
            .HasMaxLength(32)
            .IsFixedLength()
            .IsUnicode(false);
        entity.Property(auditLog => auditLog.MetadataJson)
            .HasMaxLength(AuditMetadata.MaximumSerializedLength);

        entity.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(auditLog => auditLog.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AuditLogs_Users_ActorUserId");

        entity.HasIndex(auditLog => auditLog.OccurredAtUtc)
            .HasDatabaseName("IX_AuditLogs_OccurredAtUtc")
            .IsDescending();
        entity.HasIndex(auditLog => new
            {
                auditLog.ActorUserId,
                auditLog.OccurredAtUtc,
            })
            .HasDatabaseName("IX_AuditLogs_ActorUserId_OccurredAtUtc")
            .IsDescending(false, true);
        entity.HasIndex(auditLog => new
            {
                auditLog.ResourceType,
                auditLog.ResourceId,
                auditLog.OccurredAtUtc,
            })
            .HasDatabaseName("IX_AuditLogs_ResourceType_ResourceId_OccurredAtUtc")
            .IsDescending(false, false, true);
        entity.HasIndex(auditLog => auditLog.TraceId)
            .HasDatabaseName("IX_AuditLogs_TraceId")
            .HasFilter("[TraceId] IS NOT NULL");
    }
}
