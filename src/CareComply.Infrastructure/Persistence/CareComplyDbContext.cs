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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(DefaultSchema);
        builder.ApplyConfigurationsFromAssembly(typeof(CareComplyDbContext).Assembly);
    }
}
