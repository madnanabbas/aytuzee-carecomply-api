using CareComply.Domain.Companies;
using CareComply.Domain.Forms;
using CareComply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareComply.Infrastructure.Persistence.Configurations;

internal sealed class FormTemplateConfiguration : IEntityTypeConfiguration<FormTemplate>
{
    public void Configure(EntityTypeBuilder<FormTemplate> entity)
    {
        entity.ToTable("FormTemplates", table => table.HasCheckConstraint(
            "CK_FormTemplates_Owner",
            "([OwnerType] = 'Platform' AND [CompanyId] IS NULL) OR "
            + "([OwnerType] = 'Company' AND [CompanyId] IS NOT NULL)"));
        entity.HasKey(template => template.Id).HasName("PK_FormTemplates");
        entity.Property(template => template.Id).ValueGeneratedNever();
        entity.Property(template => template.Key)
            .HasMaxLength(FormTemplate.MaximumKeyLength).IsUnicode(false).IsRequired();
        entity.Property(template => template.Name)
            .HasMaxLength(FormTemplate.MaximumNameLength).IsRequired();
        entity.Property(template => template.Description)
            .HasMaxLength(FormTemplate.MaximumDescriptionLength).IsRequired();
        entity.Property(template => template.Category)
            .HasMaxLength(FormTemplate.MaximumCategoryLength).IsRequired();
        entity.Property(template => template.OwnerType)
            .HasConversion<string>().HasMaxLength(16).IsUnicode(false).IsRequired();
        entity.Property(template => template.IsActive).HasDefaultValue(true).IsRequired();
        entity.Property(template => template.CreatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(template => template.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(template => template.RowVersion).IsRowVersion();

        entity.HasOne<Company>()
            .WithMany()
            .HasForeignKey(template => template.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_FormTemplates_Companies_CompanyId");
        entity.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(template => template.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_FormTemplates_Users_CreatedByUserId");

        entity.HasIndex(template => template.Key)
            .HasDatabaseName("UX_FormTemplates_Platform_Key")
            .IsUnique()
            .HasFilter("[OwnerType] = 'Platform'");
        entity.HasIndex(template => new { template.CompanyId, template.Key })
            .HasDatabaseName("UX_FormTemplates_Company_Key")
            .IsUnique()
            .HasFilter("[OwnerType] = 'Company'");
        entity.HasIndex(template => new { template.OwnerType, template.CompanyId, template.IsActive, template.Name })
            .HasDatabaseName("IX_FormTemplates_Owner_Active_Name");
        entity.HasIndex(template => template.CreatedByUserId)
            .HasDatabaseName("IX_FormTemplates_CreatedByUserId");
    }
}

internal sealed class FormTemplateVersionConfiguration
    : IEntityTypeConfiguration<FormTemplateVersion>
{
    public void Configure(EntityTypeBuilder<FormTemplateVersion> entity)
    {
        entity.ToTable("FormTemplateVersions", table =>
        {
            table.HasCheckConstraint(
                "CK_FormTemplateVersions_PositiveValues",
                "[VersionNumber] > 0 AND [SizeBytes] > 0 AND [PageCount] > 0");
            table.HasCheckConstraint(
                "CK_FormTemplateVersions_EffectiveDates",
                "[EffectiveTo] IS NULL OR [EffectiveFrom] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
            table.HasCheckConstraint(
                "CK_FormTemplateVersions_PublishedState",
                "([Status] = 'Draft' AND [PublishedByUserId] IS NULL AND [PublishedAtUtc] IS NULL) OR "
                + "([Status] IN ('Published', 'Retired') AND [PublishedByUserId] IS NOT NULL "
                + "AND [PublishedAtUtc] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_FormTemplateVersions_FormTechnology",
                "[FormTechnology] IN ('AcroForm', 'HybridXfaAcroForm', 'Unsupported')");
            table.HasCheckConstraint(
                "CK_FormTemplateVersions_PdfContent",
                "[ContentType] = 'application/pdf' AND LEN([StorageObjectKey]) = 32 "
                + "AND [StorageObjectKey] NOT LIKE '%[^0-9a-f]%' "
                + "AND LEN([Sha256]) = 64 AND [Sha256] NOT LIKE '%[^0-9a-f]%' ");
        });
        entity.HasKey(version => version.Id).HasName("PK_FormTemplateVersions");
        entity.Property(version => version.Id).ValueGeneratedNever();
        entity.Property(version => version.Status)
            .HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired();
        entity.Property(version => version.OriginalFileName)
            .HasMaxLength(FormTemplateVersion.MaximumOriginalFileNameLength).IsRequired();
        entity.Property(version => version.StorageObjectKey)
            .HasMaxLength(FormTemplateVersion.MaximumStorageObjectKeyLength)
            .IsUnicode(false).IsRequired();
        entity.Property(version => version.ContentType)
            .HasMaxLength(FormTemplateVersion.MaximumContentTypeLength)
            .IsUnicode(false).IsRequired();
        entity.Property(version => version.Sha256)
            .HasMaxLength(FormTemplateVersion.Sha256Length).IsUnicode(false).IsFixedLength().IsRequired();
        entity.Property(version => version.FormTechnology)
            .HasConversion<string>().HasMaxLength(32).IsUnicode(false).IsRequired();
        entity.Property(version => version.EffectiveFrom).HasColumnType("date");
        entity.Property(version => version.EffectiveTo).HasColumnType("date");
        entity.Property(version => version.UploadedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(version => version.PublishedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(version => version.RowVersion).IsRowVersion();

        entity.HasOne<FormTemplate>()
            .WithMany()
            .HasForeignKey(version => version.FormTemplateId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_FormTemplateVersions_FormTemplates_FormTemplateId");
        entity.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(version => version.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_FormTemplateVersions_Users_UploadedByUserId");
        entity.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(version => version.PublishedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_FormTemplateVersions_Users_PublishedByUserId");

        entity.HasIndex(version => new { version.FormTemplateId, version.VersionNumber })
            .HasDatabaseName("UX_FormTemplateVersions_Template_Version").IsUnique();
        entity.HasIndex(version => version.StorageObjectKey)
            .HasDatabaseName("UX_FormTemplateVersions_StorageObjectKey").IsUnique();
        entity.HasIndex(version => version.Sha256)
            .HasDatabaseName("UX_FormTemplateVersions_Sha256").IsUnique();
        entity.HasIndex(version => version.UploadedByUserId)
            .HasDatabaseName("IX_FormTemplateVersions_UploadedByUserId");
        entity.HasIndex(version => version.PublishedByUserId)
            .HasDatabaseName("IX_FormTemplateVersions_PublishedByUserId");
        entity.HasIndex(version => new { version.FormTemplateId, version.Status, version.VersionNumber })
            .HasDatabaseName("IX_FormTemplateVersions_Template_Status_Version");
    }
}

internal sealed class FormTemplateFieldMappingConfiguration
    : IEntityTypeConfiguration<FormTemplateFieldMapping>
{
    public void Configure(EntityTypeBuilder<FormTemplateFieldMapping> entity)
    {
        entity.ToTable("FormTemplateFieldMappings", table =>
        {
            table.HasCheckConstraint(
                "CK_FormTemplateFieldMappings_Binding",
                "([FieldPurpose] = 'Data' AND [BindingKey] IS NOT NULL) OR "
                + "([FieldPurpose] IN ('Manual', 'Signature', 'Attestation') AND [BindingKey] IS NULL)");
            table.HasCheckConstraint(
                "CK_FormTemplateFieldMappings_PdfFieldType",
                "[PdfFieldType] IN ('Text', 'CheckBox', 'RadioButton', 'Choice', "
                + "'Button', 'Signature', 'Unknown')");
        });
        entity.HasKey(mapping => mapping.Id).HasName("PK_FormTemplateFieldMappings");
        entity.Property(mapping => mapping.Id).ValueGeneratedNever();
        entity.Property(mapping => mapping.PdfFieldName)
            .HasMaxLength(FormTemplateFieldMapping.MaximumPdfFieldNameLength).IsRequired();
        entity.Property(mapping => mapping.PdfFieldType)
            .HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired();
        entity.Property(mapping => mapping.FieldPurpose)
            .HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired();
        entity.Property(mapping => mapping.BindingKey)
            .HasMaxLength(FormTemplateFieldMapping.MaximumBindingKeyLength).IsUnicode(false);
        entity.Property(mapping => mapping.FormatKey)
            .HasMaxLength(FormTemplateFieldMapping.MaximumFormatKeyLength).IsUnicode(false);
        entity.Property(mapping => mapping.CreatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(mapping => mapping.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(mapping => mapping.RowVersion).IsRowVersion();

        entity.HasOne<FormTemplateVersion>()
            .WithMany()
            .HasForeignKey(mapping => mapping.FormTemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_FormTemplateFieldMappings_FormTemplateVersions_FormTemplateVersionId");
        entity.HasIndex(mapping => new { mapping.FormTemplateVersionId, mapping.PdfFieldName })
            .HasDatabaseName("UX_FormTemplateFieldMappings_Version_FieldName").IsUnique();
        entity.HasIndex(mapping => mapping.BindingKey)
            .HasDatabaseName("IX_FormTemplateFieldMappings_BindingKey");
    }
}
