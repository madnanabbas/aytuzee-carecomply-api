using CareComply.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareComply.Infrastructure.Persistence.Configurations;

internal sealed class ClientServiceAgreementConfiguration
    : IEntityTypeConfiguration<ClientServiceAgreement>
{
    public void Configure(EntityTypeBuilder<ClientServiceAgreement> entity)
    {
        entity.ToTable(
            "ClientServiceAgreements",
            table => table.HasCheckConstraint(
                "CK_ClientServiceAgreements_Dates",
                "[EndDate] IS NULL OR [EndDate] >= [StartDate]"));

        entity.HasKey(agreement => agreement.Id).HasName("PK_ClientServiceAgreements");
        entity.Property(agreement => agreement.Id).ValueGeneratedNever();
        entity.Property(agreement => agreement.AgreementNumber)
            .HasMaxLength(ClientServiceAgreement.MaximumAgreementNumberLength)
            .IsUnicode(false);
        entity.Property(agreement => agreement.PayerName)
            .HasMaxLength(ClientServiceAgreement.MaximumPayerNameLength);
        entity.Property(agreement => agreement.InsuredMemberId)
            .HasMaxLength(ClientServiceAgreement.MaximumInsuredMemberIdLength)
            .IsUnicode(false);
        entity.Property(agreement => agreement.PriorAuthorizationNumber)
            .HasMaxLength(ClientServiceAgreement.MaximumPriorAuthorizationNumberLength)
            .IsUnicode(false);
        entity.Property(agreement => agreement.StartDate).HasColumnType("date");
        entity.Property(agreement => agreement.EndDate).HasColumnType("date");
        entity.Property(agreement => agreement.Status)
            .HasMaxLength(ClientServiceAgreement.MaximumStatusLength);
        entity.Property(agreement => agreement.Notes)
            .HasMaxLength(ClientServiceAgreement.MaximumNotesLength);
        entity.Property(agreement => agreement.CreatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(agreement => agreement.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(agreement => agreement.RowVersion).IsRowVersion().IsConcurrencyToken();

        entity.HasOne<Client>()
            .WithMany()
            .HasForeignKey(agreement => agreement.ClientId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ClientServiceAgreements_Clients_ClientId");

        entity.HasIndex(agreement => agreement.ClientId)
            .HasDatabaseName("IX_ClientServiceAgreements_ClientId");
        entity.HasIndex(agreement => new { agreement.ClientId, agreement.StartDate })
            .HasDatabaseName("IX_ClientServiceAgreements_ClientId_StartDate");
    }
}

internal sealed class ClientServiceAgreementLineConfiguration
    : IEntityTypeConfiguration<ClientServiceAgreementLine>
{
    public void Configure(EntityTypeBuilder<ClientServiceAgreementLine> entity)
    {
        entity.ToTable(
            "ClientServiceAgreementLines",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ClientServiceAgreementLines_MinutesPerUnit",
                    "[MinutesPerUnit] IS NULL OR [MinutesPerUnit] > 0");
                table.HasCheckConstraint(
                    "CK_ClientServiceAgreementLines_AuthorizedUnits",
                    "[AuthorizedUnits] IS NULL OR [AuthorizedUnits] >= 0");
                table.HasCheckConstraint(
                    "CK_ClientServiceAgreementLines_RatePerUnit",
                    "[RatePerUnit] IS NULL OR [RatePerUnit] >= 0");
                table.HasCheckConstraint(
                    "CK_ClientServiceAgreementLines_PlannedHoursPerVisit",
                    "[PlannedHoursPerVisit] IS NULL OR [PlannedHoursPerVisit] >= 0");
                table.HasCheckConstraint(
                    "CK_ClientServiceAgreementLines_PlannedVisitsPerWeek",
                    "[PlannedVisitsPerWeek] IS NULL OR [PlannedVisitsPerWeek] >= 0");
            });

        entity.HasKey(line => line.Id).HasName("PK_ClientServiceAgreementLines");
        entity.Property(line => line.Id).ValueGeneratedNever();
        entity.Property(line => line.ServiceCode)
            .HasMaxLength(ClientServiceAgreementLine.MaximumServiceCodeLength)
            .IsUnicode(false)
            .IsRequired();
        entity.Property(line => line.ServiceName)
            .HasMaxLength(ClientServiceAgreementLine.MaximumServiceNameLength)
            .IsRequired();
        entity.Property(line => line.UnitType)
            .HasMaxLength(ClientServiceAgreementLine.MaximumUnitTypeLength);
        entity.Property(line => line.AuthorizedUnits).HasPrecision(18, 4);
        entity.Property(line => line.RatePerUnit).HasPrecision(18, 4);
        entity.Property(line => line.PlannedHoursPerVisit).HasPrecision(18, 4);
        entity.Property(line => line.PlannedVisitsPerWeek).HasPrecision(18, 4);
        entity.Property(line => line.PrimaryDiagnosisText)
            .HasMaxLength(ClientServiceAgreementLine.MaximumDiagnosisTextLength);
        entity.Property(line => line.IcdCode)
            .HasMaxLength(ClientServiceAgreementLine.MaximumIcdCodeLength)
            .IsUnicode(false);
        entity.Property(line => line.CreatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(line => line.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(line => line.RowVersion).IsRowVersion().IsConcurrencyToken();

        entity.HasOne<ClientServiceAgreement>()
            .WithMany()
            .HasForeignKey(line => line.ClientServiceAgreementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_ClientServiceAgreementLines_ClientServiceAgreements_ClientServiceAgreementId");

        entity.HasIndex(line => line.ClientServiceAgreementId)
            .HasDatabaseName("IX_ClientServiceAgreementLines_ClientServiceAgreementId");
        entity.HasIndex(line => new { line.ClientServiceAgreementId, line.ServiceCode })
            .HasDatabaseName(
                "IX_ClientServiceAgreementLines_ClientServiceAgreementId_ServiceCode");
    }
}
