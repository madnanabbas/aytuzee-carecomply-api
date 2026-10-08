using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Clients;

internal sealed class EfClientServiceAgreementStore(
    CareComplyDbContext context,
    ITenantScopeAccessor tenantScopeAccessor) : IClientServiceAgreementStore
{
    public async Task<IReadOnlyList<ClientServiceAgreementItem>?> ListAsync(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessClientAsync(clientId, cancellationToken))
        {
            return null;
        }

        var agreements = await context.ClientServiceAgreements
            .AsNoTracking()
            .Where(agreement => agreement.ClientId == clientId)
            .OrderByDescending(agreement => agreement.StartDate)
            .ThenBy(agreement => agreement.Id)
            .ToArrayAsync(cancellationToken);
        return await MapAsync(agreements, cancellationToken);
    }

    public async Task<ClientServiceAgreementItem?> GetAsync(
        Guid clientId,
        Guid agreementId,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessClientAsync(clientId, cancellationToken))
        {
            return null;
        }

        var agreement = await context.ClientServiceAgreements
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == agreementId && item.ClientId == clientId,
                cancellationToken);
        if (agreement is null)
        {
            return null;
        }

        return (await MapAsync([agreement], cancellationToken))[0];
    }

    public async Task<ClientServiceAgreementMutation> CreateAsync(
        SaveClientServiceAgreement request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessClientAsync(request.ClientId, cancellationToken))
        {
            return new(ClientServiceAgreementMutationStatus.ClientNotFound);
        }

        var agreement = ClientServiceAgreement.Create(
            Guid.CreateVersion7(),
            request.ClientId,
            request.AgreementNumber,
            request.PayerName,
            request.InsuredMemberId,
            request.PriorAuthorizationNumber,
            request.StartDate,
            request.EndDate,
            request.Status,
            request.Notes,
            request.NowUtc);
        var lines = request.Lines.Select(line => CreateLine(agreement.Id, line, request.NowUtc)).ToArray();

        context.ClientServiceAgreements.Add(agreement);
        context.ClientServiceAgreementLines.AddRange(lines);
        await context.SaveChangesAsync(cancellationToken);

        return new(
            ClientServiceAgreementMutationStatus.Succeeded,
            Map(agreement, lines));
    }

    public async Task<ClientServiceAgreementMutation> UpdateAsync(
        UpdateClientServiceAgreement request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessClientAsync(request.ClientId, cancellationToken))
        {
            return new(ClientServiceAgreementMutationStatus.ClientNotFound);
        }

        var agreement = await context.ClientServiceAgreements.SingleOrDefaultAsync(
            item => item.Id == request.AgreementId && item.ClientId == request.ClientId,
            cancellationToken);
        if (agreement is null)
        {
            return new(ClientServiceAgreementMutationStatus.NotFound);
        }

        if (!ClientEditorVersion.Matches(agreement.RowVersion, request.EditorVersion))
        {
            return new(ClientServiceAgreementMutationStatus.Conflict);
        }

        var existingLines = await context.ClientServiceAgreementLines
            .Where(line => line.ClientServiceAgreementId == agreement.Id)
            .ToDictionaryAsync(line => line.Id, cancellationToken);
        var requestedExistingIds = request.Lines
            .Where(line => line.Id.HasValue)
            .Select(line => line.Id!.Value)
            .ToArray();
        if (requestedExistingIds.Any(id => !existingLines.ContainsKey(id)))
        {
            return new(ClientServiceAgreementMutationStatus.NotFound);
        }

        agreement.Update(
            request.AgreementNumber,
            request.PayerName,
            request.InsuredMemberId,
            request.PriorAuthorizationNumber,
            request.StartDate,
            request.EndDate,
            request.Status,
            request.Notes,
            request.NowUtc);

        foreach (var requestedLine in request.Lines)
        {
            if (requestedLine.Id is { } lineId)
            {
                UpdateLine(existingLines[lineId], requestedLine, request.NowUtc);
                continue;
            }

            var line = CreateLine(agreement.Id, requestedLine, request.NowUtc);
            existingLines.Add(line.Id, line);
            context.ClientServiceAgreementLines.Add(line);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(ClientServiceAgreementMutationStatus.Conflict);
        }

        return new(
            ClientServiceAgreementMutationStatus.Succeeded,
            Map(agreement, existingLines.Values));
    }

    private async Task<bool> CanAccessClientAsync(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        return tenant is not null
            && await context.Clients.AsNoTracking().AnyAsync(
                client => client.Id == clientId
                    && (tenant.IsPlatformAdministrator
                        || (client.CompanyId == tenant.CompanyId
                            && context.UserLocations.Any(access =>
                                access.UserId == tenant.UserId
                                && access.LocationId == client.LocationId))),
                cancellationToken);
    }

    private async Task<IReadOnlyList<ClientServiceAgreementItem>> MapAsync(
        ClientServiceAgreement[] agreements,
        CancellationToken cancellationToken)
    {
        if (agreements.Length == 0)
        {
            return [];
        }

        var agreementIds = agreements.Select(agreement => agreement.Id).ToArray();
        var lines = await context.ClientServiceAgreementLines
            .AsNoTracking()
            .Where(line => agreementIds.Contains(line.ClientServiceAgreementId))
            .OrderBy(line => line.ServiceCode)
            .ThenBy(line => line.Id)
            .ToArrayAsync(cancellationToken);
        var linesByAgreement = lines.ToLookup(line => line.ClientServiceAgreementId);
        return agreements
            .Select(agreement => Map(agreement, linesByAgreement[agreement.Id]))
            .ToArray();
    }

    private static ClientServiceAgreementItem Map(
        ClientServiceAgreement agreement,
        IEnumerable<ClientServiceAgreementLine> lines) => new(
        agreement.Id,
        agreement.AgreementNumber,
        agreement.PayerName,
        agreement.InsuredMemberId,
        agreement.PriorAuthorizationNumber,
        agreement.StartDate,
        agreement.EndDate,
        agreement.Status,
        agreement.Notes,
        ClientEditorVersion.Create(agreement.RowVersion),
        lines.OrderBy(line => line.ServiceCode).ThenBy(line => line.Id).Select(Map).ToArray());

    private static ClientServiceAgreementLineItem Map(ClientServiceAgreementLine line)
    {
        decimal? estimatedBudget = line.AuthorizedUnits.HasValue && line.RatePerUnit.HasValue
            ? ClientServiceAgreementCalculations.CalculateEstimatedBudget(
                line.AuthorizedUnits.Value,
                line.RatePerUnit.Value)
            : null;
        decimal? plannedHoursPerWeek = line.PlannedHoursPerVisit.HasValue
            && line.PlannedVisitsPerWeek.HasValue
                ? ClientServiceAgreementCalculations.CalculatePlannedHoursPerWeek(
                    line.PlannedHoursPerVisit.Value,
                    line.PlannedVisitsPerWeek.Value)
                : null;
        decimal? plannedUnitsPerWeek = plannedHoursPerWeek.HasValue
            && line.MinutesPerUnit is { } minutesPerUnit
            ? ClientServiceAgreementCalculations.CalculatePlannedUnitsPerWeek(
                plannedHoursPerWeek.Value,
                minutesPerUnit)
            : null;

        return new(
            line.Id,
            line.ServiceCode,
            line.ServiceName,
            line.UnitType,
            line.MinutesPerUnit,
            line.AuthorizedUnits,
            line.RatePerUnit,
            line.PlannedHoursPerVisit,
            line.PlannedVisitsPerWeek,
            line.PrimaryDiagnosisText,
            line.IcdCode,
            estimatedBudget,
            plannedHoursPerWeek,
            plannedUnitsPerWeek);
    }

    private static ClientServiceAgreementLine CreateLine(
        Guid agreementId,
        SaveClientServiceAgreementLine line,
        DateTimeOffset nowUtc) => ClientServiceAgreementLine.Create(
        Guid.CreateVersion7(),
        agreementId,
        line.ServiceCode,
        line.ServiceName,
        line.UnitType,
        line.MinutesPerUnit,
        line.AuthorizedUnits,
        line.RatePerUnit,
        line.PlannedHoursPerVisit,
        line.PlannedVisitsPerWeek,
        line.PrimaryDiagnosisText,
        line.IcdCode,
        nowUtc);

    private static void UpdateLine(
        ClientServiceAgreementLine existing,
        SaveClientServiceAgreementLine requested,
        DateTimeOffset nowUtc) => existing.Update(
        requested.ServiceCode,
        requested.ServiceName,
        requested.UnitType,
        requested.MinutesPerUnit,
        requested.AuthorizedUnits,
        requested.RatePerUnit,
        requested.PlannedHoursPerVisit,
        requested.PlannedVisitsPerWeek,
        requested.PrimaryDiagnosisText,
        requested.IcdCode,
        nowUtc);
}
