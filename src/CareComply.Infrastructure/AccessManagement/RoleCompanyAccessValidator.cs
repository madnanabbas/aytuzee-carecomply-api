using CareComply.Application.Abstractions.Tenancy;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.AccessManagement;

internal sealed class RoleCompanyAccessValidator(
    CareComplyDbContext context,
    ITenantScopeAccessor tenantAccessor)
{
    public async Task<Guid?> ResolveCreateCompanyAsync(
        Guid? requestedCompanyId,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantAccessor.GetAsync(cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        var targetCompanyId = tenant.IsPlatformAdministrator
            ? requestedCompanyId
            : tenant.CompanyId;
        if (targetCompanyId is null
            || (!tenant.IsPlatformAdministrator
                && requestedCompanyId is not null
                && requestedCompanyId != tenant.CompanyId)
            || !await context.Companies.AnyAsync(
                company => company.Id == targetCompanyId && company.IsActive,
                cancellationToken))
        {
            return null;
        }

        return targetCompanyId;
    }

    public async Task<bool> CanAccessAsync(
        ApplicationRole role,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantAccessor.GetAsync(cancellationToken);
        return tenant is not null
            && (tenant.IsPlatformAdministrator || role.CompanyId == tenant.CompanyId);
    }
}
