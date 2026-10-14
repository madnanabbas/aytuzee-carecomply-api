using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Clients;

internal sealed class EfClientCoverageStore(CareComplyDbContext context, ITenantScopeAccessor tenants)
    : IClientCoverageStore
{
    public async Task<IReadOnlyList<ClientCoverageItem>?> ListAsync(Guid clientId, CancellationToken ct)
    {
        if (!await CanAccessClientAsync(clientId, ct)) return null;
        var coverages = await context.ClientCoverages.AsNoTracking()
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.ManualPayerName)
            .ToArrayAsync(ct);
        var payerIds = coverages.Where(x => x.PayerId.HasValue)
            .Select(x => x.PayerId!.Value).Distinct().ToArray();
        var payerNames = await context.Payers.AsNoTracking()
            .Where(x => payerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        return coverages.Select(coverage => Map(coverage,
            coverage.PayerId.HasValue ? payerNames[coverage.PayerId.Value] : coverage.ManualPayerName!))
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.PayerName)
            .ToArray();
    }

    public async Task<ClientCoverageMutation> CreateAsync(SaveClientCoverage request, CancellationToken ct)
    {
        var companyId = await AccessibleClientCompanyAsync(request.ClientId, ct);
        if (!companyId.HasValue) return new(ClientCoverageMutationStatus.ClientNotFound);
        if (!await ValidActivePayerAsync(request.PayerId, companyId.Value, ct))
            return new(ClientCoverageMutationStatus.InvalidPayer);
        if (request.IsPrimary && await context.ClientCoverages.AnyAsync(
            x => x.ClientId == request.ClientId && x.IsPrimary, ct))
            return new(ClientCoverageMutationStatus.DuplicatePrimary);
        var coverage = ClientCoverage.Create(Guid.CreateVersion7(), request.ClientId, request.PayerId,
            request.ManualPayerName, request.MemberId, request.MedicaidId, request.EffectiveFrom,
            request.EffectiveTo, request.IsVerified, request.IsPrimary, request.NowUtc);
        context.ClientCoverages.Add(coverage);
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return new(ClientCoverageMutationStatus.DuplicatePrimary); }
        return new(ClientCoverageMutationStatus.Succeeded, await MapAsync(coverage, ct));
    }

    public async Task<ClientCoverageMutation> UpdateAsync(UpdateClientCoverage request, CancellationToken ct)
    {
        var companyId = await AccessibleClientCompanyAsync(request.ClientId, ct);
        if (!companyId.HasValue) return new(ClientCoverageMutationStatus.ClientNotFound);
        var coverage = await context.ClientCoverages.SingleOrDefaultAsync(
            x => x.Id == request.CoverageId && x.ClientId == request.ClientId, ct);
        if (coverage is null) return new(ClientCoverageMutationStatus.NotFound);
        if (!ClientEditorVersion.Matches(coverage.RowVersion, request.EditorVersion))
            return new(ClientCoverageMutationStatus.Conflict);
        if (!await ValidActivePayerAsync(request.PayerId, companyId.Value, ct))
            return new(ClientCoverageMutationStatus.InvalidPayer);
        if (request.IsPrimary && await context.ClientCoverages.AnyAsync(
            x => x.ClientId == request.ClientId && x.Id != request.CoverageId && x.IsPrimary, ct))
            return new(ClientCoverageMutationStatus.DuplicatePrimary);
        coverage.Update(request.PayerId, request.ManualPayerName, request.MemberId,
            request.MedicaidId, request.EffectiveFrom, request.EffectiveTo, request.IsVerified,
            request.IsPrimary, request.NowUtc);
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return new(ClientCoverageMutationStatus.Conflict); }
        catch (DbUpdateException) { return new(ClientCoverageMutationStatus.DuplicatePrimary); }
        return new(ClientCoverageMutationStatus.Succeeded, await MapAsync(coverage, ct));
    }

    private async Task<ClientCoverageItem> MapAsync(ClientCoverage coverage, CancellationToken ct)
    {
        var payerName = coverage.ManualPayerName;
        if (coverage.PayerId.HasValue)
        {
            payerName = await context.Payers.AsNoTracking()
                .Where(x => x.Id == coverage.PayerId.Value)
                .Select(x => x.Name)
                .SingleAsync(ct);
        }

        return Map(coverage, payerName!);
    }

    private static ClientCoverageItem Map(ClientCoverage coverage, string payerName) =>
        new(coverage.Id, coverage.PayerId, payerName, coverage.ManualPayerName,
            coverage.MemberId, coverage.MedicaidId, coverage.EffectiveFrom, coverage.EffectiveTo,
            coverage.IsVerified, coverage.IsPrimary,
            ClientEditorVersion.Create(coverage.RowVersion));

    private async Task<bool> ValidActivePayerAsync(Guid? payerId, Guid companyId, CancellationToken ct) =>
        !payerId.HasValue || await context.Payers.AsNoTracking().AnyAsync(
            x => x.Id == payerId && x.CompanyId == companyId && x.IsActive, ct);

    private async Task<bool> CanAccessClientAsync(Guid clientId, CancellationToken ct) =>
        (await AccessibleClientCompanyAsync(clientId, ct)).HasValue;

    private async Task<Guid?> AccessibleClientCompanyAsync(Guid clientId, CancellationToken ct)
    {
        var tenant = await tenants.GetAsync(ct);
        if (tenant is null) return null;
        return await context.Clients.AsNoTracking()
            .Where(client => client.Id == clientId && (tenant.IsPlatformAdministrator ||
                (client.CompanyId == tenant.CompanyId && context.UserLocations.Any(access =>
                    access.UserId == tenant.UserId && access.LocationId == client.LocationId))))
            .Select(client => (Guid?)client.CompanyId).SingleOrDefaultAsync(ct);
    }

}
