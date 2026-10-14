using CareComply.Domain.Clients;
using CareComply.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareComply.Infrastructure.Persistence.Configurations;

internal sealed class PayerConfiguration : IEntityTypeConfiguration<Payer>
{
    public void Configure(EntityTypeBuilder<Payer> entity)
    {
        entity.ToTable("Payers");
        entity.HasKey(x => x.Id).HasName("PK_Payers");
        entity.Property(x => x.Name).HasMaxLength(Payer.MaximumNameLength).IsRequired();
        entity.Property(x => x.CreatedAtUtc).HasPrecision(0);
        entity.Property(x => x.UpdatedAtUtc).HasPrecision(0);
        entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        entity.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Payers_Companies_CompanyId");
        entity.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique()
            .HasDatabaseName("UX_Payers_CompanyId_Name");
        entity.HasIndex(x => new { x.CompanyId, x.IsActive, x.Name })
            .HasDatabaseName("IX_Payers_CompanyId_Active_Name");
    }
}

internal sealed class ClientCoverageConfiguration : IEntityTypeConfiguration<ClientCoverage>
{
    public void Configure(EntityTypeBuilder<ClientCoverage> entity)
    {
        entity.ToTable("ClientCoverages", table =>
        {
            table.HasCheckConstraint("CK_ClientCoverages_PayerSelection",
                "([PayerId] IS NOT NULL AND [ManualPayerName] IS NULL) OR ([PayerId] IS NULL AND [ManualPayerName] IS NOT NULL AND LEN(LTRIM(RTRIM([ManualPayerName]))) > 0)");
            table.HasCheckConstraint("CK_ClientCoverages_Dates",
                "[EffectiveFrom] IS NULL OR [EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
        });
        entity.HasKey(x => x.Id).HasName("PK_ClientCoverages");
        entity.Property(x => x.ManualPayerName).HasMaxLength(ClientCoverage.MaximumPayerNameLength);
        entity.Property(x => x.MemberId).HasMaxLength(ClientCoverage.MaximumMemberIdLength);
        entity.Property(x => x.MedicaidId).HasMaxLength(ClientCoverage.MaximumMedicaidIdLength);
        entity.Property(x => x.CreatedAtUtc).HasPrecision(0);
        entity.Property(x => x.UpdatedAtUtc).HasPrecision(0);
        entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        entity.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ClientCoverages_Clients_ClientId");
        entity.HasOne<Payer>().WithMany().HasForeignKey(x => x.PayerId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ClientCoverages_Payers_PayerId");
        entity.HasIndex(x => x.ClientId).HasDatabaseName("IX_ClientCoverages_ClientId");
        entity.HasIndex(x => x.PayerId).HasDatabaseName("IX_ClientCoverages_PayerId");
        entity.HasIndex(x => x.ClientId).IsUnique().HasFilter("[IsPrimary] = 1")
            .HasDatabaseName("UX_ClientCoverages_OnePrimaryPerClient");
    }
}
