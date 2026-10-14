using CareComply.Application.Abstractions.Companies;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Domain.Companies;
using CareComply.Infrastructure.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Companies;

internal sealed class EfPayerStore(CareComplyDbContext context, ITenantScopeAccessor tenants) : IPayerStore
{
    public async Task<IReadOnlyList<PayerItem>?> ListAsync(bool includeInactive, CancellationToken ct)
    {
        var companyId = await CompanyIdAsync(ct);
        if (!companyId.HasValue) return null;
        return await context.Payers.AsNoTracking()
            .Where(x => x.CompanyId == companyId && (includeInactive || x.IsActive))
            .OrderBy(x => x.Name)
            .Select(x => new PayerItem(x.Id, x.Name, x.IsActive, Convert.ToBase64String(x.RowVersion)))
            .ToArrayAsync(ct);
    }

    public async Task<PayerMutation> CreateAsync(string name, DateTimeOffset nowUtc, CancellationToken ct)
    {
        var companyId = await CompanyIdAsync(ct);
        if (!companyId.HasValue) return new(PayerMutationStatus.TenantUnavailable);
        var normalized = name.Trim();
        if (await context.Payers.AnyAsync(x => x.CompanyId == companyId && x.Name == normalized, ct))
            return new(PayerMutationStatus.DuplicateName);
        var payer = Payer.Create(Guid.CreateVersion7(), companyId.Value, normalized, nowUtc);
        context.Payers.Add(payer);
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return new(PayerMutationStatus.DuplicateName); }
        return new(PayerMutationStatus.Succeeded, Map(payer));
    }

    public async Task<PayerMutation> UpdateAsync(Guid payerId, string name, bool isActive,
        string editorVersion, DateTimeOffset nowUtc, CancellationToken ct)
    {
        var companyId = await CompanyIdAsync(ct);
        if (!companyId.HasValue) return new(PayerMutationStatus.TenantUnavailable);
        var payer = await context.Payers.SingleOrDefaultAsync(
            x => x.Id == payerId && x.CompanyId == companyId, ct);
        if (payer is null) return new(PayerMutationStatus.NotFound);
        if (!ClientEditorVersion.Matches(payer.RowVersion, editorVersion))
            return new(PayerMutationStatus.Conflict);
        var normalized = name.Trim();
        if (await context.Payers.AnyAsync(
            x => x.CompanyId == companyId && x.Id != payerId && x.Name == normalized, ct))
            return new(PayerMutationStatus.DuplicateName);
        payer.Update(normalized, isActive, nowUtc);
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return new(PayerMutationStatus.Conflict); }
        catch (DbUpdateException) { return new(PayerMutationStatus.DuplicateName); }
        return new(PayerMutationStatus.Succeeded, Map(payer));
    }

    private async Task<Guid?> CompanyIdAsync(CancellationToken ct) =>
        (await tenants.GetAsync(ct))?.CompanyId;

    private static PayerItem Map(Payer payer) =>
        new(payer.Id, payer.Name, payer.IsActive, ClientEditorVersion.Create(payer.RowVersion));
}
