using CareComply.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareComply.Infrastructure.Persistence.Configurations;

internal sealed class ClientGuestSessionConfiguration : IEntityTypeConfiguration<ClientGuestSession>
{
    public void Configure(EntityTypeBuilder<ClientGuestSession> entity)
    {
        entity.ToTable("ClientGuestSessions", table => table.HasCheckConstraint(
            "CK_ClientGuestSessions_ExpiresAfterCreation", "[ExpiresAtUtc] > [CreatedAtUtc]"));
        entity.HasKey(value => value.Id).HasName("PK_ClientGuestSessions");
        entity.Property(value => value.Id).ValueGeneratedNever();
        entity.Property(value => value.SessionTokenHash).HasColumnType("binary(32)").IsRequired();
        entity.Property(value => value.CsrfTokenHash).HasColumnType("binary(32)").IsRequired();
        entity.Property(value => value.CreatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(value => value.LastActivityAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(value => value.ExpiresAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(value => value.RevokedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(value => value.CompletedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(value => value.RowVersion).IsRowVersion().IsConcurrencyToken();
        entity.HasIndex(value => value.SessionTokenHash)
            .HasDatabaseName("UX_ClientGuestSessions_SessionTokenHash").IsUnique();
        entity.HasIndex(value => value.ClientGuestInvitationId)
            .HasDatabaseName("UX_ClientGuestSessions_InvitationId").IsUnique();
        entity.HasOne<ClientGuestInvitation>().WithOne()
            .HasForeignKey<ClientGuestSession>(value => value.ClientGuestInvitationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ClientGuestSessions_ClientGuestInvitations_InvitationId");
    }
}
