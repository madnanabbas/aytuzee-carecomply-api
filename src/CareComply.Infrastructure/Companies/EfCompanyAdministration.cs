using CareComply.Application.Abstractions.Companies;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Domain.Companies;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CareComply.Infrastructure.Identity;

namespace CareComply.Infrastructure.Companies;

internal sealed class EfCompanyAdministration(CareComplyDbContext context, ITenantScopeAccessor tenantAccessor, TimeProvider timeProvider) : ICompanyAdministration
{
    public async Task<IReadOnlyList<CompanySummary>> ListAsync(CancellationToken ct)
    {
        if (!await IsPlatformAdmin(ct)) return [];
        return await context.Companies.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new CompanySummary(x.Id, x.Name, x.Code, x.IsActive,
                context.Users.Count(u => u.CompanyId == x.Id), context.Clients.Count(c => c.CompanyId == x.Id),
                Convert.ToBase64String(x.RowVersion))).ToArrayAsync(ct);
    }

    public async Task<CompanySummary?> GetAsync(Guid id, CancellationToken ct) =>
        !await IsPlatformAdmin(ct) ? null : await Query(id).SingleOrDefaultAsync(ct);

    public async Task<CompanyMutation> CreateAsync(string name, string code, CancellationToken ct)
    {
        if (!await IsPlatformAdmin(ct)) return new(CompanyMutationStatus.Forbidden);
        var normalizedCode = code.Trim().ToUpperInvariant();
        if (await context.Companies.AnyAsync(x => x.Code == normalizedCode, ct)) return new(CompanyMutationStatus.DuplicateCode);
        var company = Company.Create(Guid.CreateVersion7(), name, normalizedCode, timeProvider.GetUtcNow());
        context.Companies.Add(company);
        context.CompanyLocations.Add(CompanyLocation.Create(Guid.CreateVersion7(), company.Id, "Main", true, timeProvider.GetUtcNow()));
        foreach (var locationName in DemoLocationFixtures.Names)
            context.CompanyLocations.Add(CompanyLocation.Create(Guid.CreateVersion7(), company.Id, locationName, false, timeProvider.GetUtcNow()));
        await context.SaveChangesAsync(ct);
        return new(CompanyMutationStatus.Succeeded, await GetAsync(company.Id, ct));
    }

    public async Task<CompanyMutation> UpdateAsync(Guid id, string name, string code, bool isActive, string editorVersion, CancellationToken ct)
    {
        if (!await IsPlatformAdmin(ct)) return new(CompanyMutationStatus.Forbidden);
        var company = await context.Companies.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (company is null) return new(CompanyMutationStatus.NotFound);
        byte[] version;
        try { version = Convert.FromBase64String(editorVersion); } catch (FormatException) { return new(CompanyMutationStatus.Conflict); }
        context.Entry(company).Property(x => x.RowVersion).OriginalValue = version;
        var normalizedCode = code.Trim().ToUpperInvariant();
        if (await context.Companies.AnyAsync(x => x.Id != id && x.Code == normalizedCode, ct)) return new(CompanyMutationStatus.DuplicateCode);
        company.Update(name, normalizedCode, isActive, timeProvider.GetUtcNow());
        try { await context.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return new(CompanyMutationStatus.Conflict); }
        return new(CompanyMutationStatus.Succeeded, await GetAsync(id, ct));
    }

    public async Task<CompanyMutation> DeleteAsync(Guid id, CancellationToken ct)
    {
        if (!await IsPlatformAdmin(ct)) return new(CompanyMutationStatus.Forbidden);
        var company = await context.Companies.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (company is null) return new(CompanyMutationStatus.NotFound);
        if (await context.Users.AnyAsync(x => x.CompanyId == id, ct) || await context.Clients.AnyAsync(x => x.CompanyId == id, ct)) return new(CompanyMutationStatus.InUse);
        context.Companies.Remove(company); await context.SaveChangesAsync(ct);
        return new(CompanyMutationStatus.Succeeded);
    }

    private IQueryable<CompanySummary> Query(Guid id) => context.Companies.AsNoTracking().Where(x => x.Id == id)
        .Select(x => new CompanySummary(x.Id, x.Name, x.Code, x.IsActive, context.Users.Count(u => u.CompanyId == x.Id), context.Clients.Count(c => c.CompanyId == x.Id), Convert.ToBase64String(x.RowVersion)));
    private async Task<bool> IsPlatformAdmin(CancellationToken ct) => (await tenantAccessor.GetAsync(ct))?.IsPlatformAdministrator == true;
}
