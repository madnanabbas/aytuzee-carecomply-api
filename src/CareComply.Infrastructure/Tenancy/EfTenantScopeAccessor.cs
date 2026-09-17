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

        cached = await context.Users.AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive)
            .Select(user => new TenantScope(
                user.Id,
                user.CompanyId,
                user.IsPlatformAdministrator ? null : context.Companies
                    .Where(company => company.Id == user.CompanyId && company.IsActive)
                    .Select(company => company.Name).SingleOrDefault(),
                user.IsPlatformAdministrator))
            .SingleOrDefaultAsync(cancellationToken);

        if (cached is { IsPlatformAdministrator: false, CompanyId: null } ||
            cached is { IsPlatformAdministrator: false, CompanyName: null }) cached = null;

        return cached;
    }
}
