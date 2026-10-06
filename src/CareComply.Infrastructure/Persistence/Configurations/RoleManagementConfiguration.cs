using CareComply.Domain.AccessManagement;
using CareComply.Domain.Navigation;
using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareComply.Infrastructure.Persistence.Configurations;

internal sealed class RoleProfileConfiguration : IEntityTypeConfiguration<RoleProfile>
{
    public void Configure(EntityTypeBuilder<RoleProfile> entity)
    {
        entity.ToTable("RoleProfiles");
        entity.HasKey(profile => profile.RoleId).HasName("PK_RoleProfiles");
        entity.Property(profile => profile.Description).HasMaxLength(RoleDefinition.MaximumDescriptionLength).IsRequired();
        entity.HasOne<ApplicationRole>()
            .WithOne()
            .HasForeignKey<RoleProfile>(profile => profile.RoleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_RoleProfiles_Roles_RoleId");
    }
}

internal sealed class RolePermissionFeatureLinkConfiguration
    : IEntityTypeConfiguration<RolePermissionFeatureLink>
{
    public void Configure(EntityTypeBuilder<RolePermissionFeatureLink> entity)
    {
        entity.ToTable("RolePermissionFeatureLinks");
        entity.HasKey(link => new { link.FeatureId, link.PermissionId })
            .HasName("PK_RolePermissionFeatureLinks");
        entity.HasOne<NavigationFeature>()
            .WithMany()
            .HasForeignKey(link => link.FeatureId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_RolePermissionFeatureLinks_NavigationFeatures_FeatureId");
        entity.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(link => link.PermissionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_RolePermissionFeatureLinks_Permissions_PermissionId");
        entity.HasIndex(link => link.PermissionId)
            .HasDatabaseName("UX_RolePermissionFeatureLinks_PermissionId")
            .IsUnique();
    }
}
