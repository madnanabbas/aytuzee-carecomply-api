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
        if (!await CanAccessClientAsync(clientId, cancellationToken)) return null;
        var agreements = await context.ClientServiceAgreements.AsNoTracking()
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.StartDate).ThenBy(x => x.Id)
            .ToArrayAsync(cancellationToken);
        return await MapAsync(agreements, cancellationToken);
    }

    public async Task<ClientServiceAgreementItem?> GetAsync(
        Guid clientId,
        Guid agreementId,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessClientAsync(clientId, cancellationToken)) return null;
        var agreement = await context.ClientServiceAgreements.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == agreementId && x.ClientId == clientId,
                cancellationToken);
        return agreement is null ? null : (await MapAsync([agreement], cancellationToken))[0];
    }

    public async Task<ClientServiceAgreementMutation> CreateAsync(
        SaveClientServiceAgreement request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessClientAsync(request.ClientId, cancellationToken))
            return new(ClientServiceAgreementMutationStatus.ClientNotFound);

        var selection = await ResolvePayerSelectionAsync(request.ClientId, request.PayerId,
            request.ClientCoverageId, request.PayerName, cancellationToken);
        if (selection.Status.HasValue) return new(selection.Status.Value);

        var agreement = ClientServiceAgreement.Create(
            Guid.CreateVersion7(), request.ClientId, selection.PayerId, selection.CoverageId,
            request.AgreementNumber, selection.PayerName,
            request.InsuredMemberId, request.PriorAuthorizationNumber, request.StartDate,
            request.EndDate, request.Status, request.Priority, request.IsDummy, request.Notes,
            request.NowUtc);
        var lines = request.Lines.Select(x => CreateLine(agreement.Id, x, request.NowUtc)).ToArray();
        var diagnoses = request.Lines.Zip(lines)
            .SelectMany(pair => pair.First.Diagnoses.Select(
                x => CreateDiagnosis(pair.Second.Id, x, request.NowUtc)))
            .ToArray();

        context.ClientServiceAgreements.Add(agreement);
        context.ClientServiceAgreementLines.AddRange(lines);
        context.ClientServiceAgreementLineDiagnoses.AddRange(diagnoses);
        await context.SaveChangesAsync(cancellationToken);
        return new(
            ClientServiceAgreementMutationStatus.Succeeded,
            (await MapAsync([agreement], cancellationToken))[0]);
    }

    public async Task<ClientServiceAgreementMutation> UpdateAsync(
        UpdateClientServiceAgreement request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessClientAsync(request.ClientId, cancellationToken))
            return new(ClientServiceAgreementMutationStatus.ClientNotFound);

        var selection = await ResolvePayerSelectionAsync(request.ClientId, request.PayerId,
            request.ClientCoverageId, request.PayerName, cancellationToken);
        if (selection.Status.HasValue) return new(selection.Status.Value);

        var agreement = await context.ClientServiceAgreements.SingleOrDefaultAsync(
            x => x.Id == request.AgreementId && x.ClientId == request.ClientId,
            cancellationToken);
        if (agreement is null) return new(ClientServiceAgreementMutationStatus.NotFound);
        if (!ClientEditorVersion.Matches(agreement.RowVersion, request.EditorVersion))
            return new(ClientServiceAgreementMutationStatus.Conflict);

        var existingLines = await context.ClientServiceAgreementLines
            .Where(x => x.ClientServiceAgreementId == agreement.Id)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (request.Lines.Where(x => x.Id.HasValue).Select(x => x.Id!.Value)
            .Any(id => !existingLines.ContainsKey(id)))
            return new(ClientServiceAgreementMutationStatus.NotFound);

        var existingDiagnoses = await context.ClientServiceAgreementLineDiagnoses
            .Where(x => existingLines.Keys.Contains(x.ClientServiceAgreementLineId))
            .ToArrayAsync(cancellationToken);
        var diagnosesByLine = existingDiagnoses.GroupBy(x => x.ClientServiceAgreementLineId)
            .ToDictionary(group => group.Key, group => group.ToDictionary(x => x.Id));

        agreement.Update(
            selection.PayerId, selection.CoverageId, request.AgreementNumber, selection.PayerName,
            request.InsuredMemberId,
            request.PriorAuthorizationNumber, request.StartDate, request.EndDate, request.Status,
            request.Priority, request.IsDummy, request.Notes, request.NowUtc);

        foreach (var requestedLine in request.Lines)
        {
            if (requestedLine.Id is { } lineId)
            {
                var lineDiagnoses = diagnosesByLine.GetValueOrDefault(lineId) ?? [];
                if (requestedLine.Diagnoses.Where(x => x.Id.HasValue)
                    .Any(x => !lineDiagnoses.ContainsKey(x.Id!.Value)))
                    return new(ClientServiceAgreementMutationStatus.NotFound);
                UpdateLine(existingLines[lineId], requestedLine, request.NowUtc);
                SynchronizeDiagnoses(lineId, lineDiagnoses, requestedLine.Diagnoses, request.NowUtc);
            }
            else
            {
                var line = CreateLine(agreement.Id, requestedLine, request.NowUtc);
                existingLines.Add(line.Id, line);
                context.ClientServiceAgreementLines.Add(line);
                context.ClientServiceAgreementLineDiagnoses.AddRange(requestedLine.Diagnoses.Select(
                    x => CreateDiagnosis(line.Id, x, request.NowUtc)));
            }
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
            (await MapAsync([agreement], cancellationToken))[0]);
    }

    private async Task<bool> CanAccessClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        return tenant is not null && await context.Clients.AsNoTracking().AnyAsync(
            client => client.Id == clientId
                && (tenant.IsPlatformAdministrator
                    || (client.CompanyId == tenant.CompanyId
                        && context.UserLocations.Any(access => access.UserId == tenant.UserId
                            && access.LocationId == client.LocationId))),
            cancellationToken);
    }

    private async Task<IReadOnlyList<ClientServiceAgreementItem>> MapAsync(
        ClientServiceAgreement[] agreements,
        CancellationToken cancellationToken)
    {
        if (agreements.Length == 0) return [];
        var agreementIds = agreements.Select(x => x.Id).ToArray();
        var lines = await context.ClientServiceAgreementLines.AsNoTracking()
            .Where(x => agreementIds.Contains(x.ClientServiceAgreementId))
            .OrderBy(x => x.ServiceCode).ThenBy(x => x.Id).ToArrayAsync(cancellationToken);
        var lineIds = lines.Select(x => x.Id).ToArray();
        var diagnoses = await context.ClientServiceAgreementLineDiagnoses.AsNoTracking()
            .Where(x => lineIds.Contains(x.ClientServiceAgreementLineId))
            .OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Id)
            .ToArrayAsync(cancellationToken);
        var linesByAgreement = lines.ToLookup(x => x.ClientServiceAgreementId);
        var diagnosesByLine = diagnoses.ToLookup(x => x.ClientServiceAgreementLineId);
        var warningsByAgreement = await FindOverlapWarningsAsync(
            agreements,
            linesByAgreement,
            cancellationToken);
        return agreements.Select(x => Map(
            x,
            linesByAgreement[x.Id],
            diagnosesByLine,
            warningsByAgreement.GetValueOrDefault(x.Id) ?? [])).ToArray();
    }

    private static ClientServiceAgreementItem Map(
        ClientServiceAgreement agreement,
        IEnumerable<ClientServiceAgreementLine> lines,
        ILookup<Guid, ClientServiceAgreementLineDiagnosis> diagnosesByLine,
        IReadOnlyList<ClientServiceAgreementWarning> warnings) => new(
        agreement.Id, agreement.PayerId, agreement.ClientCoverageId, agreement.AgreementNumber,
        agreement.PayerName, agreement.InsuredMemberId,
        agreement.PriorAuthorizationNumber, agreement.StartDate, agreement.EndDate,
        agreement.Status, agreement.Priority, agreement.IsDummy, agreement.Notes,
        ClientEditorVersion.Create(agreement.RowVersion),
        lines.OrderBy(x => x.ServiceCode).ThenBy(x => x.Id)
            .Select(x => Map(x, diagnosesByLine[x.Id])).ToArray(),
        warnings);

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ClientServiceAgreementWarning>>>
        FindOverlapWarningsAsync(
            ClientServiceAgreement[] agreements,
            ILookup<Guid, ClientServiceAgreementLine> linesByAgreement,
            CancellationToken cancellationToken)
    {
        var activeAgreements = agreements
            .Where(x => x.Status == ClientServiceAgreementStatus.Active)
            .ToArray();
        if (activeAgreements.Length == 0) return new Dictionary<Guid, IReadOnlyList<ClientServiceAgreementWarning>>();

        var clientIds = activeAgreements.Select(x => x.ClientId).Distinct().ToArray();
        var candidates = await (
            from agreement in context.ClientServiceAgreements.AsNoTracking()
            join line in context.ClientServiceAgreementLines.AsNoTracking()
                on agreement.Id equals line.ClientServiceAgreementId
            where clientIds.Contains(agreement.ClientId)
                && agreement.Status == ClientServiceAgreementStatus.Active
            select new OverlapCandidate(
                agreement.Id,
                agreement.ClientId,
                line.Id,
                line.ServiceCode,
                line.StartDate,
                line.EndDate))
            .ToArrayAsync(cancellationToken);

        var result = new Dictionary<Guid, IReadOnlyList<ClientServiceAgreementWarning>>();
        foreach (var agreement in activeAgreements)
        {
            var warnings = (
                from line in linesByAgreement[agreement.Id]
                from candidate in candidates
                where candidate.ClientId == agreement.ClientId
                    && candidate.AgreementId != agreement.Id
                    && string.Equals(
                        candidate.ServiceCode,
                        line.ServiceCode,
                        StringComparison.OrdinalIgnoreCase)
                    && DatesOverlap(line.StartDate, line.EndDate, candidate.StartDate, candidate.EndDate)
                let overlapStart = line.StartDate >= candidate.StartDate
                    ? line.StartDate
                    : candidate.StartDate
                let overlapEnd = EarliestEnd(line.EndDate, candidate.EndDate)
                orderby line.ServiceCode, overlapStart, candidate.AgreementId, candidate.LineId
                select new ClientServiceAgreementWarning(
                    "SERVICE_LINE_OVERLAP",
                    line.ServiceCode,
                    line.Id,
                    candidate.AgreementId,
                    candidate.LineId,
                    overlapStart,
                    overlapEnd))
                .ToArray();
            if (warnings.Length > 0) result.Add(agreement.Id, warnings);
        }

        return result;
    }

    private static bool DatesOverlap(
        DateOnly leftStart,
        DateOnly? leftEnd,
        DateOnly rightStart,
        DateOnly? rightEnd) =>
        leftStart <= (rightEnd ?? DateOnly.MaxValue)
        && rightStart <= (leftEnd ?? DateOnly.MaxValue);

    private static DateOnly? EarliestEnd(DateOnly? left, DateOnly? right) =>
        left.HasValue && right.HasValue
            ? (left.Value <= right.Value ? left : right)
            : left ?? right;

    private static ClientServiceAgreementLineItem Map(
        ClientServiceAgreementLine line,
        IEnumerable<ClientServiceAgreementLineDiagnosis> diagnoses)
    {
        var estimatedBudget = line.AuthorizedUnits.HasValue && line.RatePerUnit.HasValue
            ? ClientServiceAgreementCalculations.CalculateEstimatedBudget(
                line.AuthorizedUnits.Value, line.RatePerUnit.Value)
            : (decimal?)null;
        var plannedHoursPerWeek = line.PlannedHoursPerVisit.HasValue
            && line.PlannedVisitsPerWeek.HasValue
            ? ClientServiceAgreementCalculations.CalculatePlannedHoursPerWeek(
                line.PlannedHoursPerVisit.Value, line.PlannedVisitsPerWeek.Value)
            : (decimal?)null;
        var plannedUnitsPerWeek = plannedHoursPerWeek.HasValue
            && line.MinutesPerUnit is { } minutesPerUnit
            ? ClientServiceAgreementCalculations.CalculatePlannedUnitsPerWeek(
                plannedHoursPerWeek.Value, minutesPerUnit)
            : (decimal?)null;
        return new(
            line.Id, line.ServiceCode, line.ServiceName, line.UnitType, line.MinutesPerUnit,
            line.AuthorizedUnits, line.RatePerUnit, line.StartDate, line.EndDate,
            line.RateEffectiveFrom, line.RateEffectiveTo, line.AuthorizedBudgetAmount,
            line.PlannedHoursPerVisit, line.PlannedVisitsPerWeek,
            diagnoses.Select(Map).ToArray(), estimatedBudget, plannedHoursPerWeek,
            plannedUnitsPerWeek);
    }

    private static ClientServiceAgreementLineDiagnosisItem Map(
        ClientServiceAgreementLineDiagnosis diagnosis) => new(
        diagnosis.Id, diagnosis.DiagnosisText, diagnosis.IcdCode, diagnosis.IsPrimary);

    private async Task<PayerSelection> ResolvePayerSelectionAsync(
        Guid clientId, Guid? payerId, Guid? coverageId, string? payerName, CancellationToken ct)
    {
        var companyId = await context.Clients.AsNoTracking().Where(x => x.Id == clientId)
            .Select(x => x.CompanyId).SingleAsync(ct);
        if (coverageId.HasValue)
        {
            var coverage = await context.ClientCoverages.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == coverageId && x.ClientId == clientId, ct);
            if (coverage is null || (payerId.HasValue && coverage.PayerId != payerId))
                return new(ClientServiceAgreementMutationStatus.InvalidCoverage, null, null, null);
            if (coverage.PayerId.HasValue)
            {
                var payer = await context.Payers.AsNoTracking().SingleOrDefaultAsync(
                    x => x.Id == coverage.PayerId && x.CompanyId == companyId, ct);
                if (payer is null)
                    return new(ClientServiceAgreementMutationStatus.InvalidCoverage, null, null, null);
                return new(null, payer.Id, coverage.Id, payer.Name);
            }
            return new(null, null, coverage.Id, coverage.ManualPayerName);
        }

        if (payerId.HasValue)
        {
            var payer = await context.Payers.AsNoTracking().SingleOrDefaultAsync(
                x => x.Id == payerId && x.CompanyId == companyId, ct);
            if (payer is null)
                return new(ClientServiceAgreementMutationStatus.InvalidPayer, null, null, null);
            return new(null, payer.Id, null, payer.Name);
        }
        return new(null, null, null, payerName);
    }

    private sealed record PayerSelection(ClientServiceAgreementMutationStatus? Status,
        Guid? PayerId, Guid? CoverageId, string? PayerName);

    private sealed record OverlapCandidate(
        Guid AgreementId,
        Guid ClientId,
        Guid LineId,
        string ServiceCode,
        DateOnly StartDate,
        DateOnly? EndDate);

    private static ClientServiceAgreementLine CreateLine(
        Guid agreementId,
        SaveClientServiceAgreementLine line,
        DateTimeOffset nowUtc) => ClientServiceAgreementLine.Create(
        Guid.CreateVersion7(), agreementId, line.ServiceCode, line.ServiceName, line.UnitType,
        line.MinutesPerUnit, line.AuthorizedUnits, line.RatePerUnit, line.StartDate, line.EndDate,
        line.RateEffectiveFrom, line.RateEffectiveTo, line.AuthorizedBudgetAmount,
        line.PlannedHoursPerVisit, line.PlannedVisitsPerWeek, nowUtc);

    private static ClientServiceAgreementLineDiagnosis CreateDiagnosis(
        Guid lineId,
        SaveClientServiceAgreementLineDiagnosis diagnosis,
        DateTimeOffset nowUtc) => ClientServiceAgreementLineDiagnosis.Create(
        Guid.CreateVersion7(), lineId, diagnosis.DiagnosisText, diagnosis.IcdCode,
        diagnosis.IsPrimary, nowUtc);

    private static void UpdateLine(
        ClientServiceAgreementLine existing,
        SaveClientServiceAgreementLine requested,
        DateTimeOffset nowUtc) => existing.Update(
        requested.ServiceCode, requested.ServiceName, requested.UnitType,
        requested.MinutesPerUnit, requested.AuthorizedUnits, requested.RatePerUnit,
        requested.StartDate, requested.EndDate, requested.RateEffectiveFrom,
        requested.RateEffectiveTo, requested.AuthorizedBudgetAmount,
        requested.PlannedHoursPerVisit, requested.PlannedVisitsPerWeek, nowUtc);

    private void SynchronizeDiagnoses(
        Guid lineId,
        IReadOnlyDictionary<Guid, ClientServiceAgreementLineDiagnosis> existing,
        IReadOnlyList<SaveClientServiceAgreementLineDiagnosis> requested,
        DateTimeOffset nowUtc)
    {
        var requestedIds = requested.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();
        context.ClientServiceAgreementLineDiagnoses.RemoveRange(
            existing.Values.Where(x => !requestedIds.Contains(x.Id)));
        foreach (var diagnosis in requested)
        {
            if (diagnosis.Id is { } diagnosisId)
                existing[diagnosisId].Update(
                    diagnosis.DiagnosisText, diagnosis.IcdCode, diagnosis.IsPrimary, nowUtc);
            else
                context.ClientServiceAgreementLineDiagnoses.Add(
                    CreateDiagnosis(lineId, diagnosis, nowUtc));
        }
    }
}
