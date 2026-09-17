using CareComply.Application.Abstractions.Companies;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Domain.Companies;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Companies;

internal sealed class EfLocationAdministration(CareComplyDbContext context, ITenantScopeAccessor tenants, TimeProvider timeProvider) : ILocationAdministration
{
    public async Task<IReadOnlyList<CompanyLocationItem>> ListAsync(Guid? companyId, CancellationToken ct)
    {
        var tenant = await tenants.GetAsync(ct); if (tenant is null) return [];
        var target = tenant.IsPlatformAdministrator ? companyId : tenant.CompanyId;
        if (target is null) return [];
        return await context.CompanyLocations.AsNoTracking().Where(x => x.CompanyId == target && x.IsActive)
            .OrderByDescending(x => x.IsMain).ThenBy(x => x.Name)
            .Select(x => new CompanyLocationItem(x.Id, x.CompanyId, x.Name, x.IsMain, x.IsActive)).ToArrayAsync(ct);
    }
    public async Task<CompanyLocationItem?> CreateAsync(Guid? companyId, string name, CancellationToken ct)
    {
        var tenant = await tenants.GetAsync(ct); if (tenant is null) return null;
        var target = tenant.IsPlatformAdministrator ? companyId : tenant.CompanyId;
        if (target is null || !await context.Companies.AnyAsync(x => x.Id == target && x.IsActive, ct)) return null;
        var normalized = name.Trim();
        if (await context.CompanyLocations.AnyAsync(x => x.CompanyId == target && x.Name == normalized, ct)) return null;
        var location = CompanyLocation.Create(Guid.CreateVersion7(), target.Value, normalized, false, timeProvider.GetUtcNow());
        context.CompanyLocations.Add(location); await context.SaveChangesAsync(ct);
        return new(location.Id, location.CompanyId, location.Name, location.IsMain, location.IsActive);
    }
}
