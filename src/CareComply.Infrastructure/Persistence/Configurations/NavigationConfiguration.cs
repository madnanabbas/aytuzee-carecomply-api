using CareComply.Domain.Navigation;
using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Navigation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareComply.Infrastructure.Persistence.Configurations;

internal sealed class NavigationModuleConfiguration : IEntityTypeConfiguration<NavigationModule>
{
    public void Configure(EntityTypeBuilder<NavigationModule> entity)
    {
        entity.ToTable("NavigationModules", table => table.HasCheckConstraint(
            "CK_NavigationModules_SortOrder", "[SortOrder] >= 0"));
        entity.HasKey(module => module.Id).HasName("PK_NavigationModules");
        entity.Property(module => module.Id).ValueGeneratedNever();
        entity.Property(module => module.Key).HasMaxLength(100).IsUnicode(false).IsRequired();
        entity.Property(module => module.Name).HasMaxLength(100).IsRequired();
        entity.HasIndex(module => module.Key).HasDatabaseName("UX_NavigationModules_Key").IsUnique();
        entity.HasIndex(module => new { module.IsActive, module.SortOrder, module.Key })
            .HasDatabaseName("IX_NavigationModules_Active_Order");
        entity.HasData(NavigationSeedData.Modules.Select(module => new
        {
            module.Id,
            module.Key,
            module.Name,
            module.SortOrder,
            module.IsActive,
        }));
    }
}

internal sealed class NavigationFeatureConfiguration : IEntityTypeConfiguration<NavigationFeature>
{
    public void Configure(EntityTypeBuilder<NavigationFeature> entity)
    {
        entity.ToTable("NavigationFeatures", table => table.HasCheckConstraint(
            "CK_NavigationFeatures_SortOrder", "[SortOrder] >= 0"));
        entity.HasKey(feature => feature.Id).HasName("PK_NavigationFeatures");
        entity.Property(feature => feature.Id).ValueGeneratedNever();
        entity.Property(feature => feature.Key).HasMaxLength(100).IsUnicode(false).IsRequired();
        entity.Property(feature => feature.Name).HasMaxLength(100).IsRequired();
        entity.Property(feature => feature.Route).HasMaxLength(200).IsUnicode(false).IsRequired();
        entity.Property(feature => feature.IconKey).HasMaxLength(100).IsUnicode(false).IsRequired();
        entity.HasOne<NavigationModule>()
            .WithMany()
            .HasForeignKey(feature => feature.ModuleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_NavigationFeatures_NavigationModules_ModuleId");
        entity.HasOne<NavigationFeature>()
            .WithMany()
            .HasForeignKey(feature => feature.ParentFeatureId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_NavigationFeatures_NavigationFeatures_ParentFeatureId");
        entity.HasIndex(feature => feature.Key).HasDatabaseName("UX_NavigationFeatures_Key").IsUnique();
        entity.HasIndex(feature => feature.Route).HasDatabaseName("UX_NavigationFeatures_Route").IsUnique();
        entity.HasIndex(feature => new
            {
                feature.ModuleId,
                feature.ParentFeatureId,
                feature.IsActive,
                feature.SortOrder,
                feature.Key,
            })
            .HasDatabaseName("IX_NavigationFeatures_Module_Parent_Active_Order");
        entity.HasData(NavigationSeedData.Features.Select(feature => new
        {
            feature.Id,
            feature.ModuleId,
            feature.ParentFeatureId,
            feature.Key,
            feature.Name,
            feature.Route,
            feature.IconKey,
            feature.SortOrder,
            feature.IsActive,
        }));
    }
}

internal sealed class NavigationFeaturePermissionConfiguration
    : IEntityTypeConfiguration<NavigationFeaturePermissionLink>
{
    public void Configure(EntityTypeBuilder<NavigationFeaturePermissionLink> entity)
    {
        entity.ToTable("NavigationFeaturePermissions");
        entity.HasKey(value => new { value.FeatureId, value.PermissionId })
            .HasName("PK_NavigationFeaturePermissions");
        entity.HasOne<NavigationFeature>()
            .WithMany()
            .HasForeignKey(value => value.FeatureId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_NavigationFeaturePermissions_NavigationFeatures_FeatureId");
        entity.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(value => value.PermissionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_NavigationFeaturePermissions_Permissions_PermissionId");
        entity.HasIndex(value => value.PermissionId)
            .HasDatabaseName("IX_NavigationFeaturePermissions_PermissionId");
        entity.HasData(NavigationSeedData.FeaturePermissions.Select(value => new
        {
            value.FeatureId,
            value.PermissionId,
        }));
    }
}
