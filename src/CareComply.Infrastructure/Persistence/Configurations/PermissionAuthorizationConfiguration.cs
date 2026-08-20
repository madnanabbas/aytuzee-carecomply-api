using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareComply.Infrastructure.Persistence.Configurations;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> entity)
    {
        entity.ToTable("Permissions");
        entity.HasKey(permission => permission.Id)
            .HasName("PK_Permissions");
        entity.Property(permission => permission.Id)
            .ValueGeneratedNever();
        entity.Property(permission => permission.Name)
            .HasMaxLength(100)
            .IsRequired();
        entity.HasIndex(permission => permission.Name)
            .HasDatabaseName("UX_Permissions_Name")
            .IsUnique();
        entity.HasData(PermissionSeedData.All.Select(permission => new
        {
            permission.Id,
            permission.Name,
        }));
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> entity)
    {
        entity.ToTable("RolePermissions");
        entity.HasKey(rolePermission => new
            {
                rolePermission.RoleId,
                rolePermission.PermissionId,
            })
            .HasName("PK_RolePermissions");

        entity.HasOne<ApplicationRole>()
            .WithMany()
            .HasForeignKey(rolePermission => rolePermission.RoleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_RolePermissions_Roles_RoleId");

        entity.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(rolePermission => rolePermission.PermissionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_RolePermissions_Permissions_PermissionId");

        entity.HasIndex(rolePermission => rolePermission.PermissionId)
            .HasDatabaseName("IX_RolePermissions_PermissionId");
    }
}
