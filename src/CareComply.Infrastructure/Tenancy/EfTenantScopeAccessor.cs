using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Tenancy;

internal sealed class EfTenantScopeAccessor(
    ICurrentActor currentActor,
    CareComplyDbContext context) : ITenantScopeAccessor
{
    private TenantScope? cached;
    private bool loaded;

    public async Task<TenantScope?> GetAsync(CancellationToken cancellationToken)
    {
        if (loaded) return cached;
        loaded = true;
        if (!currentActor.IsAuthenticated || currentActor.UserId is not { } userId) return null;

        cached = await (
            from user in context.Users.AsNoTracking()
            join company in context.Companies.AsNoTracking() on user.CompanyId equals company.Id
            where user.Id == userId && user.IsActive && company.IsActive
            select new TenantScope(
                user.Id,
                company.Id,
                company.Name,
                user.IsPlatformAdministrator))
            .SingleOrDefaultAsync(cancellationToken);

        return cached;
    }
}
