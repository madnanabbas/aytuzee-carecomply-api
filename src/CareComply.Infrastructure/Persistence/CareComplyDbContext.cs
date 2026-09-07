using CareComply.Domain.Clients;
using CareComply.Domain.Companies;
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


    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientAdditionalProfile> ClientAdditionalProfiles => Set<ClientAdditionalProfile>();
    public DbSet<ClientService> ClientServices => Set<ClientService>();
    public DbSet<ClientLanguagePreference> ClientLanguagePreferences => Set<ClientLanguagePreference>();
    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();
    public DbSet<ClientHospitalization> ClientHospitalizations => Set<ClientHospitalization>();
    public DbSet<ClientTodo> ClientTodos => Set<ClientTodo>();
    public DbSet<ClientCaregiver> ClientCaregivers => Set<ClientCaregiver>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(DefaultSchema);
        builder.ApplyConfigurationsFromAssembly(typeof(CareComplyDbContext).Assembly);
    }
}
