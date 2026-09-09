using CareComply.Infrastructure.Identity;
using CareComply.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareComply.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> entity)
    {
        entity.ToTable("Users");
        entity.Property(user => user.FirstName).HasMaxLength(100).IsRequired();
        entity.Property(user => user.LastName).HasMaxLength(100).IsRequired();
        entity.Property(user => user.CompanyId).IsRequired(false);
        entity.Property(user => user.IsPlatformAdministrator).HasDefaultValue(false).IsRequired();
        entity.Property(user => user.IsActive).HasDefaultValue(true).IsRequired();
        entity.Property(user => user.IsEmployee).HasDefaultValue(false).IsRequired();
        entity.Property(user => user.JobTitle).HasMaxLength(100).IsRequired();
        entity.Property(user => user.RequiresMfa).HasDefaultValue(false).IsRequired();
        entity.Property(user => user.MfaMethod)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(UserMfaMethod.Authenticator)
            .IsRequired();
        entity.Property(user => user.CreatedAtUtc).IsRequired();
        entity.Property(user => user.UpdatedAtUtc).IsRequired();
        entity.Property(user => user.Email).HasMaxLength(256).IsRequired();
        entity.Property(user => user.NormalizedEmail).HasMaxLength(256).IsRequired();
        entity.Property(user => user.UserName).HasMaxLength(256).IsRequired();
        entity.Property(user => user.NormalizedUserName).HasMaxLength(256).IsRequired();
        entity.Property(user => user.PasswordHash).HasMaxLength(1024);
        entity.Property(user => user.SecurityStamp).HasMaxLength(64);
        entity.Property(user => user.ConcurrencyStamp).HasMaxLength(64);
        entity.Property(user => user.PhoneNumber).HasMaxLength(32);

        entity.HasIndex(user => user.NormalizedUserName)
            .HasDatabaseName("UX_Users_NormalizedUserName")
            .IsUnique();
        entity.HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("UX_Users_NormalizedEmail")
            .IsUnique();
        entity.HasIndex(user => new { user.CompanyId, user.IsActive })
            .HasDatabaseName("IX_Users_CompanyId_IsActive");
    }
}

internal sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> entity)
    {
        entity.ToTable("Roles");
        entity.Property(role => role.Name).HasMaxLength(256).IsRequired();
        entity.Property(role => role.NormalizedName).HasMaxLength(256).IsRequired();
        entity.Property(role => role.ConcurrencyStamp).HasMaxLength(64);
        entity.Property(role => role.CompanyId).IsRequired();
        entity.HasOne<CareComply.Domain.Companies.Company>().WithMany().HasForeignKey(role => role.CompanyId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Roles_Companies_CompanyId");
        entity.HasIndex(role => new { role.CompanyId, role.Name }).HasDatabaseName("IX_Roles_CompanyId_Name");
        entity.HasIndex(role => role.NormalizedName)
            .HasDatabaseName("UX_Roles_NormalizedName")
            .IsUnique();
    }
}

internal sealed class IdentityUserRoleConfiguration
    : IEntityTypeConfiguration<IdentityUserRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> entity) =>
        entity.ToTable("UserRoles");
}

internal sealed class IdentityUserClaimConfiguration
    : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> entity)
    {
        entity.ToTable("UserClaims");
        entity.Property(claim => claim.ClaimType).HasMaxLength(256);
        entity.Property(claim => claim.ClaimValue).HasMaxLength(1024);
    }
}

internal sealed class IdentityUserLoginConfiguration
    : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> entity)
    {
        entity.ToTable("UserLogins");
        entity.Property(login => login.LoginProvider).HasMaxLength(128);
        entity.Property(login => login.ProviderKey).HasMaxLength(128);
        entity.Property(login => login.ProviderDisplayName).HasMaxLength(256);
    }
}

internal sealed class IdentityRoleClaimConfiguration
    : IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> entity)
    {
        entity.ToTable("RoleClaims");
        entity.Property(claim => claim.ClaimType).HasMaxLength(256);
        entity.Property(claim => claim.ClaimValue).HasMaxLength(1024);
    }
}

internal sealed class IdentityUserTokenConfiguration
    : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> entity)
    {
        entity.ToTable("UserTokens");
        entity.Property(token => token.LoginProvider).HasMaxLength(128);
        entity.Property(token => token.Name).HasMaxLength(128);
        entity.Property(token => token.Value).HasMaxLength(2048);
    }
}
