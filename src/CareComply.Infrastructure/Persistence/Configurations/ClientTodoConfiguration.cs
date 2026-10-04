using CareComply.Domain.Clients;
using CareComply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareComply.Infrastructure.Persistence.Configurations;

internal sealed class ClientTodoConfiguration : IEntityTypeConfiguration<ClientTodo>
{
    public void Configure(EntityTypeBuilder<ClientTodo> entity)
    {
        entity.ToTable(
            "ClientTodos",
            table =>
            {
                table.HasCheckConstraint("CK_ClientTodos_Priority", "[Priority] BETWEEN 1 AND 4");
                table.HasCheckConstraint("CK_ClientTodos_Status", "[Status] BETWEEN 1 AND 4");
                table.HasCheckConstraint(
                    "CK_ClientTodos_Completion",
                    "([Status] = 3 AND [CompletedByUserId] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL) OR ([Status] <> 3 AND [CompletedByUserId] IS NULL AND [CompletedAtUtc] IS NULL)"
                );
            }
        );
        entity.HasKey(x => x.Id).HasName("PK_ClientTodos");
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.Title).HasMaxLength(ClientTodo.MaximumTitleLength).IsRequired();
        entity
            .Property(x => x.Description)
            .HasMaxLength(ClientTodo.MaximumDescriptionLength)
            .IsRequired();
        entity.Property(x => x.Priority).HasConversion<int>();
        entity.Property(x => x.Status).HasConversion<int>();
        entity.Property(x => x.DueDate).HasColumnType("date");
        entity.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(x => x.CompletedAtUtc).HasColumnType("datetimeoffset(0)");
        entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        entity
            .HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
        entity
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.CompletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity
            .HasIndex(x => new { x.ClientId, x.Status })
            .HasDatabaseName("IX_ClientTodos_ClientId_Status");
        entity
            .HasIndex(x => new { x.AssignedToUserId, x.Status })
            .HasDatabaseName("IX_ClientTodos_AssignedToUserId_Status");
        entity
            .HasIndex(x => new { x.DueDate, x.Status })
            .HasDatabaseName("IX_ClientTodos_DueDate_Status");
    }
}
