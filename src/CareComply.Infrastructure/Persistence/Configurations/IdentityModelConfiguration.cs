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
        entity.Property(user => user.MiddleName).HasMaxLength(100).IsRequired();
        entity.Property(user => user.PreferredName).HasMaxLength(100).IsRequired();
        entity.Property(user => user.SecondaryJobTitle).HasMaxLength(100).IsRequired();
        entity.Property(user => user.Gender).HasMaxLength(32).IsRequired();
        entity.Property(user => user.SsnProtected).HasMaxLength(512).IsRequired();
        entity.Property(user => user.SecondaryPhone).HasMaxLength(32).IsRequired();
        entity.Property(user => user.LanguagesCsv).HasMaxLength(500).IsRequired();
        entity.Property(user => user.HoursPerMonth).HasPrecision(8, 2);
        entity.Property(user => user.Umpi).HasMaxLength(50).IsRequired();
        entity.Property(user => user.Npi).HasMaxLength(10).IsRequired();
        entity.Property(user => user.LicenseId).HasMaxLength(100).IsRequired();
        entity.Property(user => user.StreetAddress).HasMaxLength(200).IsRequired();
        entity.Property(user => user.Apartment).HasMaxLength(50).IsRequired();
        entity.Property(user => user.City).HasMaxLength(100).IsRequired();
        entity.Property(user => user.State).HasMaxLength(2).IsRequired();
        entity.Property(user => user.PostalCode).HasMaxLength(10).IsRequired();
        entity.Property(user => user.County).HasMaxLength(100).IsRequired();
        entity.Property(user => user.EmployeeNotes).HasMaxLength(2000).IsRequired();
        entity.Property(user => user.CareProgram).HasMaxLength(40).IsRequired();
        entity.Property(user => user.PreviousName).HasMaxLength(200).IsRequired();
        entity.Property(user => user.HeightInches).HasPrecision(5, 2);
        entity.Property(user => user.WeightPounds).HasPrecision(6, 2);
        entity.Property(user => user.HairColor).HasMaxLength(50).IsRequired();
        entity.Property(user => user.EyeColor).HasMaxLength(50).IsRequired();
        entity.Property(user => user.BirthPlace).HasMaxLength(200).IsRequired();
        entity.Property(user => user.CfssCertificationNumber).HasMaxLength(100).IsRequired();
        entity.Property(user => user.AvailableDaysCsv).HasMaxLength(100).IsRequired();
        entity.Property(user => user.PreviousAddressesJson).HasMaxLength(8000).IsRequired();
        entity.Property(user => user.MaritalStatus).HasMaxLength(50).IsRequired();
        entity.Property(user => user.Ethnicity).HasMaxLength(100).IsRequired();
        entity.Property(user => user.QualificationsCsv).HasMaxLength(1000).IsRequired();
        entity.Property(user => user.SmokerRestrictionExplanation).HasMaxLength(500).IsRequired();
        entity.Property(user => user.MilitaryServiceExplanation).HasMaxLength(500).IsRequired();
        entity.Property(user => user.Race).HasMaxLength(100).IsRequired();
        entity.Property(user => user.DemographicDescription).HasMaxLength(500).IsRequired();
        entity.Property(user => user.PetRestrictionExplanation).HasMaxLength(500).IsRequired();
        entity.Property(user => user.ClientTypesCsv).HasMaxLength(1000).IsRequired();
        entity.Property(user => user.OtherEmployerName).HasMaxLength(200).IsRequired();
        entity.Property(user => user.OtherEmploymentDuration).HasMaxLength(100).IsRequired();
        entity.Property(user => user.PreviousWorkExperience).HasMaxLength(2000).IsRequired();
        entity.Property(user => user.EmergencyContactName).HasMaxLength(200).IsRequired();
        entity.Property(user => user.EmergencyContactRelationship).HasMaxLength(100).IsRequired();
        entity.Property(user => user.EmergencyContactPhone).HasMaxLength(32).IsRequired();
        entity.Property(user => user.EmergencyContactEmail).HasMaxLength(320).IsRequired();
        entity.Property(user => user.EmergencyContactAddress).HasMaxLength(300).IsRequired();
        entity.Property(user => user.TrainingName).HasMaxLength(200).IsRequired();
        entity.Property(user => user.TrainingProvider).HasMaxLength(200).IsRequired();
        entity.Property(user => user.TrainingCertificateNumber).HasMaxLength(100).IsRequired();
        entity.Property(user => user.PayerName).HasMaxLength(200).IsRequired();
        entity.Property(user => user.PayerMemberId).HasMaxLength(100).IsRequired();
        entity.Property(user => user.PayerGroupNumber).HasMaxLength(100).IsRequired();
        entity.Property(user => user.PayerPlanName).HasMaxLength(200).IsRequired();
        entity.Property(user => user.PayerPhone).HasMaxLength(32).IsRequired();
        entity.Property(user => user.EvvEmail).HasMaxLength(320).IsRequired();
        entity.Property(user => user.EvvPasswordProtected).HasMaxLength(1024).IsRequired();
        entity.Property(user => user.IvrPinProtected).HasMaxLength(1024).IsRequired();
        entity.Property(user => user.EvvIvrAuthorization).HasMaxLength(100).IsRequired();
        entity.Property(user => user.EmployeeTier).HasMaxLength(100).IsRequired();
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
