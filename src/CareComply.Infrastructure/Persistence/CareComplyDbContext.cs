using CareComply.Domain.Clients;
using CareComply.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Persistence;

public sealed class CareComplyDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private const string DefaultSchema = "dbo";

    public CareComplyDbContext(DbContextOptions<CareComplyDbContext> options)
        : base(options)
    {
    }


    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientAdditionalProfile> ClientAdditionalProfiles => Set<ClientAdditionalProfile>();
    public DbSet<ClientService> ClientServices => Set<ClientService>();
    public DbSet<ClientLanguagePreference> ClientLanguagePreferences => Set<ClientLanguagePreference>();
    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();
    public DbSet<ClientHospitalization> ClientHospitalizations => Set<ClientHospitalization>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(DefaultSchema);
        builder.ApplyConfigurationsFromAssembly(typeof(CareComplyDbContext).Assembly);
    }
}
