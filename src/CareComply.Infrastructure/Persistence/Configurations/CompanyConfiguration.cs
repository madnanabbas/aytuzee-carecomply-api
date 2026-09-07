using CareComply.Domain.Companies;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareComply.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> entity)
    {
        entity.ToTable("Companies");
        entity.HasKey(company => company.Id).HasName("PK_Companies");
        entity.Property(company => company.Id).ValueGeneratedNever();
        entity.Property(company => company.Name).HasMaxLength(Company.MaximumNameLength).IsRequired();
        entity.Property(company => company.Code).HasMaxLength(Company.MaximumCodeLength).IsUnicode(false).IsRequired();
        entity.Property(company => company.IsActive).HasDefaultValue(true).IsRequired();
        entity.Property(company => company.CreatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(company => company.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(company => company.RowVersion).IsRowVersion();
        entity.HasIndex(company => company.Code).HasDatabaseName("UX_Companies_Code").IsUnique();
        entity.HasIndex(company => company.Name).HasDatabaseName("IX_Companies_Name");
        entity.HasData(new
        {
            Id = TenantDefaults.DefaultCompanyId,
            Name = TenantDefaults.DefaultCompanyName,
            Code = TenantDefaults.DefaultCompanyCode,
            IsActive = true,
            CreatedAtUtc = new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero),
            UpdatedAtUtc = new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero),
        });

        entity.HasMany<ApplicationUser>()
            .WithOne()
            .HasForeignKey(user => user.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Users_Companies_CompanyId");
    }
}
