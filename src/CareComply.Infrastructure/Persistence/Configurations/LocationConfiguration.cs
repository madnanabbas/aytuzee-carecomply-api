using CareComply.Domain.Clients;
using CareComply.Domain.Companies;
using CareComply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareComply.Infrastructure.Persistence.Configurations;

internal sealed class CompanyLocationConfiguration : IEntityTypeConfiguration<CompanyLocation>
{
    public void Configure(EntityTypeBuilder<CompanyLocation> entity)
    {
        entity.ToTable("CompanyLocations"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.Name).HasMaxLength(CompanyLocation.MaximumNameLength).IsRequired();
        entity.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique();
        entity.HasIndex(x => new { x.CompanyId, x.IsMain }).HasFilter("[IsMain] = 1").IsUnique();
        entity.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ApplicationUserLocationConfiguration : IEntityTypeConfiguration<ApplicationUserLocation>
{
    public void Configure(EntityTypeBuilder<ApplicationUserLocation> entity)
    {
        entity.ToTable("UserLocations"); entity.HasKey(x => new { x.UserId, x.LocationId });
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<CompanyLocation>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ClientLocationConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> entity)
    {
        entity.Property(x => x.LocationId).IsRequired();
        entity.HasOne<CompanyLocation>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => new { x.CompanyId, x.LocationId, x.Status });
    }
}
