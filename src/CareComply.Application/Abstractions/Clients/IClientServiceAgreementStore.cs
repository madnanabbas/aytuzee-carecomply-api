namespace CareComply.Application.Abstractions.Clients;

public interface IClientServiceAgreementStore
{
    Task<IReadOnlyList<ClientServiceAgreementItem>?> ListAsync(
        Guid clientId,
        CancellationToken cancellationToken);

    Task<ClientServiceAgreementItem?> GetAsync(
        Guid clientId,
        Guid agreementId,
        CancellationToken cancellationToken);

    Task<ClientServiceAgreementMutation> CreateAsync(
        SaveClientServiceAgreement request,
        CancellationToken cancellationToken);

    Task<ClientServiceAgreementMutation> UpdateAsync(
        UpdateClientServiceAgreement request,
        CancellationToken cancellationToken);
}

public sealed record ClientServiceAgreementItem(
    Guid Id,
    string? AgreementNumber,
    string? PayerName,
    string? InsuredMemberId,
    string? PriorAuthorizationNumber,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Status,
    string? Notes,
    string EditorVersion,
    IReadOnlyList<ClientServiceAgreementLineItem> Lines);

public sealed record ClientServiceAgreementLineItem(
    Guid Id,
    string ServiceCode,
    string ServiceName,
    string? UnitType,
    int? MinutesPerUnit,
    decimal? AuthorizedUnits,
    decimal? RatePerUnit,
    decimal? PlannedHoursPerVisit,
    decimal? PlannedVisitsPerWeek,
    string? PrimaryDiagnosisText,
    string? IcdCode,
    decimal? EstimatedBudget,
    decimal? PlannedHoursPerWeek,
    decimal? PlannedUnitsPerWeek);

public sealed record SaveClientServiceAgreement(
    Guid ClientId,
    string? AgreementNumber,
    string? PayerName,
    string? InsuredMemberId,
    string? PriorAuthorizationNumber,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Status,
    string? Notes,
    IReadOnlyList<SaveClientServiceAgreementLine> Lines,
    DateTimeOffset NowUtc);

public sealed record UpdateClientServiceAgreement(
    Guid ClientId,
    Guid AgreementId,
    string? AgreementNumber,
    string? PayerName,
    string? InsuredMemberId,
    string? PriorAuthorizationNumber,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Status,
    string? Notes,
    string EditorVersion,
    IReadOnlyList<SaveClientServiceAgreementLine> Lines,
    DateTimeOffset NowUtc);

public sealed record SaveClientServiceAgreementLine(
    Guid? Id,
    string ServiceCode,
    string ServiceName,
    string? UnitType,
    int? MinutesPerUnit,
    decimal? AuthorizedUnits,
    decimal? RatePerUnit,
    decimal? PlannedHoursPerVisit,
    decimal? PlannedVisitsPerWeek,
    string? PrimaryDiagnosisText,
    string? IcdCode);

public enum ClientServiceAgreementMutationStatus
{
    Succeeded,
    ClientNotFound,
    NotFound,
    Conflict,
}

public sealed record ClientServiceAgreementMutation(
    ClientServiceAgreementMutationStatus Status,
    ClientServiceAgreementItem? Item = null);
