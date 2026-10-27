using CareComply.Domain.Clients;
using CareComply.Domain.Companies;
using CareComply.Domain.Forms;
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
    public DbSet<CompanyLocation> CompanyLocations => Set<CompanyLocation>();
    public DbSet<Payer> Payers => Set<Payer>();
    public DbSet<ApplicationUserLocation> UserLocations => Set<ApplicationUserLocation>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientGuestInvitation> ClientGuestInvitations => Set<ClientGuestInvitation>();
    public DbSet<ClientGuestSession> ClientGuestSessions => Set<ClientGuestSession>();
    public DbSet<ClientAdditionalProfile> ClientAdditionalProfiles => Set<ClientAdditionalProfile>();
    public DbSet<ClientService> ClientServices => Set<ClientService>();
    public DbSet<ClientLanguagePreference> ClientLanguagePreferences => Set<ClientLanguagePreference>();
    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();
    public DbSet<ClientHospitalization> ClientHospitalizations => Set<ClientHospitalization>();
    public DbSet<ClientTodo> ClientTodos => Set<ClientTodo>();
    public DbSet<ClientCaregiver> ClientCaregivers => Set<ClientCaregiver>();
    public DbSet<ClientCoverage> ClientCoverages => Set<ClientCoverage>();
    public DbSet<ClientServiceAgreement> ClientServiceAgreements => Set<ClientServiceAgreement>();
    public DbSet<ClientServiceAgreementLine> ClientServiceAgreementLines => Set<ClientServiceAgreementLine>();
    public DbSet<ClientServiceAgreementLineDiagnosis> ClientServiceAgreementLineDiagnoses =>
        Set<ClientServiceAgreementLineDiagnosis>();
    public DbSet<FormTemplate> FormTemplates => Set<FormTemplate>();
    public DbSet<FormTemplateVersion> FormTemplateVersions => Set<FormTemplateVersion>();
    public DbSet<FormTemplateFieldMapping> FormTemplateFieldMappings =>
        Set<FormTemplateFieldMapping>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(DefaultSchema);
        builder.ApplyConfigurationsFromAssembly(typeof(CareComplyDbContext).Assembly);
    }
}
