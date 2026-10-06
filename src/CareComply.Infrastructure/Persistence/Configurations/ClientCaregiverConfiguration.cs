using CareComply.Domain.Clients;
using CareComply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareComply.Infrastructure.Persistence.Configurations;

internal sealed class ClientCaregiverConfiguration : IEntityTypeConfiguration<ClientCaregiver>
{
    public void Configure(EntityTypeBuilder<ClientCaregiver> entity)
    {
        entity.ToTable("ClientCaregivers"); entity.HasKey(x => x.Id);
        entity.Property(x => x.Services).HasMaxLength(ClientCaregiver.MaximumServicesLength).IsRequired();
        entity.Property(x => x.Notes).HasMaxLength(ClientCaregiver.MaximumNotesLength).IsRequired();
        entity.Property(x => x.RelationshipToClient).HasMaxLength(ClientCaregiver.MaximumRelationshipLength).IsRequired();
        entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(16).IsUnicode(false);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsUnicode(false);
        entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        entity.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ClientGuestInvitation>().WithMany().HasForeignKey(x => x.CreatedByGuestInvitationId).OnDelete(DeleteBehavior.Restrict);
        entity.ToTable(table => table.HasCheckConstraint(
            "CK_ClientCaregivers_Creator",
            "([CreatedByUserId] IS NULL AND [CreatedByGuestInvitationId] IS NOT NULL) OR ([CreatedByUserId] IS NOT NULL AND [CreatedByGuestInvitationId] IS NULL)"));
        entity.HasIndex(x => new { x.ClientId, x.Status });
        entity.HasIndex(x => new { x.ClientId, x.UserId }).IsUnique().HasFilter("[Status] = 'Active'");
        entity.HasIndex(x => new { x.ClientId, x.Type }).IsUnique().HasFilter("[Status] = 'Active' AND [Type] = 'Primary'");
    }
}
