using CareComply.Domain.Clients;
using CareComply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareComply.Infrastructure.Persistence.Configurations;

internal sealed class ClientGuestInvitationConfiguration : IEntityTypeConfiguration<ClientGuestInvitation>
{
    public void Configure(EntityTypeBuilder<ClientGuestInvitation> entity)
    {
        entity.ToTable("ClientGuestInvitations", table =>
            table.HasCheckConstraint(
                "CK_ClientGuestInvitations_ExpiresAfterCreation",
                "[ExpiresAtUtc] > [CreatedAtUtc]"));
        entity.HasKey(invitation => invitation.Id).HasName("PK_ClientGuestInvitations");
        entity.Property(invitation => invitation.Id).ValueGeneratedNever();
        entity.Property(invitation => invitation.CompanyId).IsRequired();
        entity.Property(invitation => invitation.ClientName)
            .HasMaxLength(ClientGuestInvitation.MaximumClientNameLength).IsRequired();
        entity.Property(invitation => invitation.Email)
            .HasMaxLength(ClientGuestInvitation.MaximumEmailLength).IsUnicode(false).IsRequired();
        entity.Property(invitation => invitation.MobileNumber)
            .HasMaxLength(ClientGuestInvitation.MaximumMobileNumberLength).IsUnicode(false);
        entity.Property(invitation => invitation.TokenHash).HasColumnType("binary(32)").IsRequired();
        entity.Property(invitation => invitation.ExpiresAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(invitation => invitation.CreatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(invitation => invitation.SentAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(invitation => invitation.OpenedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(invitation => invitation.RedeemedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(invitation => invitation.ClientCreatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(invitation => invitation.CompletedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(invitation => invitation.RevokedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(invitation => invitation.RowVersion).IsRowVersion().IsConcurrencyToken();

        entity.HasIndex(invitation => invitation.TokenHash)
            .HasDatabaseName("UX_ClientGuestInvitations_TokenHash").IsUnique();
        entity.HasIndex(invitation => new { invitation.CompanyId, invitation.ExpiresAtUtc })
            .HasDatabaseName("IX_ClientGuestInvitations_CompanyId_ExpiresAtUtc");
        entity.HasIndex(invitation => invitation.CreatedClientId)
            .HasDatabaseName("UX_ClientGuestInvitations_CreatedClientId")
            .HasFilter("[CreatedClientId] IS NOT NULL").IsUnique();

        entity.HasOne<CareComply.Domain.Companies.Company>().WithMany()
            .HasForeignKey(invitation => invitation.CompanyId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ClientGuestInvitations_Companies_CompanyId");
        entity.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(invitation => invitation.CreatedByUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ClientGuestInvitations_Users_CreatedByUserId");
        entity.HasOne<Client>().WithMany()
            .HasForeignKey(invitation => invitation.CreatedClientId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ClientGuestInvitations_Clients_CreatedClientId");
    }
}
