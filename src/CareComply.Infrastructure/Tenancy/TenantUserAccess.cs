using CareComply.Application.Abstractions.Tenancy;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Tenancy;

internal sealed class TenantUserAccess(
    ITenantScopeAccessor tenantScopeAccessor,
    CareComplyDbContext context)
{
    public async Task<bool> CanManageAsync(Guid targetUserId, CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null) return false;

        return await context.Users.AsNoTracking().AnyAsync(
            user => user.Id == targetUserId
                && (tenant.IsPlatformAdministrator
                    || (user.CompanyId == tenant.CompanyId && !user.IsPlatformAdministrator)),
            cancellationToken);
    }
}
