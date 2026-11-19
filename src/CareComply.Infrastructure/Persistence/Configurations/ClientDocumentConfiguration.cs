using CareComply.Domain.Clients;
using CareComply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareComply.Infrastructure.Persistence.Configurations;

internal sealed class ClientDocumentConfiguration : IEntityTypeConfiguration<ClientDocument>
{
    public void Configure(EntityTypeBuilder<ClientDocument> entity)
    {
        entity.ToTable("ClientDocuments", table =>
            table.HasCheckConstraint("CK_ClientDocuments_Status", "[Status] IN (1, 2)"));
        entity.HasKey(item => item.Id).HasName("PK_ClientDocuments");
        entity.Property(item => item.Id).ValueGeneratedNever();
        entity.Property(item => item.Category).HasMaxLength(ClientDocument.MaximumCategoryLength).IsRequired();
        entity.Property(item => item.Title).HasMaxLength(ClientDocument.MaximumTitleLength).IsRequired();
        entity.Property(item => item.Description).HasMaxLength(ClientDocument.MaximumDescriptionLength).IsRequired();
        entity.Property(item => item.ExpiryDate).HasColumnType("date");
        entity.Property(item => item.ReviewDate).HasColumnType("date");
        entity.Property(item => item.Status).HasConversion<int>();
        entity.Property(item => item.CreatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(item => item.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        entity.HasOne<Client>().WithMany().HasForeignKey(item => item.ClientId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(item => new { item.CompanyId, item.ClientId }).HasDatabaseName("IX_ClientDocuments_CompanyId_ClientId");
        entity.HasIndex(item => new { item.ClientId, item.Status, item.Category }).HasDatabaseName("IX_ClientDocuments_ClientId_Status_Category");
        entity.HasIndex(item => item.CurrentVersionId).HasDatabaseName("IX_ClientDocuments_CurrentVersionId");
    }
}

internal sealed class ClientDocumentVersionConfiguration : IEntityTypeConfiguration<ClientDocumentVersion>
{
    public void Configure(EntityTypeBuilder<ClientDocumentVersion> entity)
    {
        entity.ToTable("ClientDocumentVersions", table =>
            table.HasCheckConstraint("CK_ClientDocumentVersions_VersionNumber", "[VersionNumber] >= 1"));
        entity.HasKey(item => item.Id).HasName("PK_ClientDocumentVersions");
        entity.Property(item => item.Id).ValueGeneratedNever();
        entity.Property(item => item.StorageObjectKey).HasMaxLength(32).IsUnicode(false).IsRequired();
        entity.Property(item => item.OriginalFileName).HasMaxLength(ClientDocumentVersion.MaximumOriginalFileNameLength).IsRequired();
        entity.Property(item => item.ContentType).HasMaxLength(ClientDocumentVersion.MaximumContentTypeLength).IsUnicode(false).IsRequired();
        entity.Property(item => item.Sha256).HasMaxLength(ClientDocumentVersion.Sha256Length).IsUnicode(false).IsFixedLength().IsRequired();
        entity.Property(item => item.UploadedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.HasOne<ClientDocument>().WithMany().HasForeignKey(item => item.DocumentId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Client>().WithMany().HasForeignKey(item => item.ClientId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(item => new { item.DocumentId, item.VersionNumber }).IsUnique().HasDatabaseName("UX_ClientDocumentVersions_DocumentId_VersionNumber");
        entity.HasIndex(item => item.DocumentId).IsUnique().HasFilter("[IsCurrent] = 1").HasDatabaseName("UX_ClientDocumentVersions_DocumentId_Current");
        entity.HasIndex(item => item.StorageObjectKey).IsUnique().HasDatabaseName("UX_ClientDocumentVersions_StorageObjectKey");
        entity.HasIndex(item => item.Sha256).HasDatabaseName("IX_ClientDocumentVersions_Sha256");
        entity.HasIndex(item => new { item.ClientId, item.Sha256 }).IsUnique()
            .HasDatabaseName("UX_ClientDocumentVersions_ClientId_Sha256");
    }
}
