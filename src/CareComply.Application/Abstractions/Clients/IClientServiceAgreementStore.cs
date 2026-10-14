using CareComply.Domain.Clients;

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
    Guid? PayerId,
    Guid? ClientCoverageId,
    string? AgreementNumber,
    string? PayerName,
    string? InsuredMemberId,
    string? PriorAuthorizationNumber,
    DateOnly StartDate,
    DateOnly? EndDate,
    ClientServiceAgreementStatus? Status,
    ClientServiceAgreementPriority? Priority,
    bool IsDummy,
    string? Notes,
    string EditorVersion,
    IReadOnlyList<ClientServiceAgreementLineItem> Lines,
    IReadOnlyList<ClientServiceAgreementWarning> Warnings)
{
    public ClientServiceAgreementItem(Guid id, string? agreementNumber, string? payerName,
        string? insuredMemberId, string? priorAuthorizationNumber, DateOnly startDate,
        DateOnly? endDate, ClientServiceAgreementStatus? status,
        ClientServiceAgreementPriority? priority, bool isDummy, string? notes,
        string editorVersion, IReadOnlyList<ClientServiceAgreementLineItem> lines)
        : this(id, null, null, agreementNumber, payerName, insuredMemberId,
            priorAuthorizationNumber, startDate, endDate, status, priority, isDummy, notes,
            editorVersion, lines, []) { }
}

public sealed record ClientServiceAgreementWarning(
    string Code,
    string ServiceCode,
    Guid ServiceLineId,
    Guid OverlappingAgreementId,
    Guid OverlappingLineId,
    DateOnly OverlapStartDate,
    DateOnly? OverlapEndDate);

public sealed record ClientServiceAgreementLineItem(
    Guid Id,
    string ServiceCode,
    string ServiceName,
    ClientServiceAgreementUnitType? UnitType,
    int? MinutesPerUnit,
    decimal? AuthorizedUnits,
    decimal? RatePerUnit,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateOnly? RateEffectiveFrom,
    DateOnly? RateEffectiveTo,
    decimal? AuthorizedBudgetAmount,
    decimal? PlannedHoursPerVisit,
    decimal? PlannedVisitsPerWeek,
    IReadOnlyList<ClientServiceAgreementLineDiagnosisItem> Diagnoses,
    decimal? EstimatedBudget,
    decimal? PlannedHoursPerWeek,
    decimal? PlannedUnitsPerWeek);

public sealed record ClientServiceAgreementLineDiagnosisItem(
    Guid Id,
    string? DiagnosisText,
    string? IcdCode,
    bool IsPrimary);

public sealed record SaveClientServiceAgreement(
    Guid ClientId,
    Guid? PayerId,
    Guid? ClientCoverageId,
    string? AgreementNumber,
    string? PayerName,
    string? InsuredMemberId,
    string? PriorAuthorizationNumber,
    DateOnly StartDate,
    DateOnly? EndDate,
    ClientServiceAgreementStatus? Status,
    ClientServiceAgreementPriority? Priority,
    bool IsDummy,
    string? Notes,
    IReadOnlyList<SaveClientServiceAgreementLine> Lines,
    DateTimeOffset NowUtc)
{
    public SaveClientServiceAgreement(Guid clientId, string? agreementNumber, string? payerName,
        string? insuredMemberId, string? priorAuthorizationNumber, DateOnly startDate,
        DateOnly? endDate, ClientServiceAgreementStatus? status,
        ClientServiceAgreementPriority? priority, bool isDummy, string? notes,
        IReadOnlyList<SaveClientServiceAgreementLine> lines, DateTimeOffset nowUtc)
        : this(clientId, null, null, agreementNumber, payerName, insuredMemberId,
            priorAuthorizationNumber, startDate, endDate, status, priority, isDummy, notes,
            lines, nowUtc) { }
}

public sealed record UpdateClientServiceAgreement(
    Guid ClientId,
    Guid AgreementId,
    Guid? PayerId,
    Guid? ClientCoverageId,
    string? AgreementNumber,
    string? PayerName,
    string? InsuredMemberId,
    string? PriorAuthorizationNumber,
    DateOnly StartDate,
    DateOnly? EndDate,
    ClientServiceAgreementStatus? Status,
    ClientServiceAgreementPriority? Priority,
    bool IsDummy,
    string? Notes,
    string EditorVersion,
    IReadOnlyList<SaveClientServiceAgreementLine> Lines,
    DateTimeOffset NowUtc)
{
    public UpdateClientServiceAgreement(Guid clientId, Guid agreementId,
        string? agreementNumber, string? payerName, string? insuredMemberId,
        string? priorAuthorizationNumber, DateOnly startDate, DateOnly? endDate,
        ClientServiceAgreementStatus? status, ClientServiceAgreementPriority? priority,
        bool isDummy, string? notes, string editorVersion,
        IReadOnlyList<SaveClientServiceAgreementLine> lines, DateTimeOffset nowUtc)
        : this(clientId, agreementId, null, null, agreementNumber, payerName,
            insuredMemberId, priorAuthorizationNumber, startDate, endDate, status, priority,
            isDummy, notes, editorVersion, lines, nowUtc) { }
}

public sealed record SaveClientServiceAgreementLine(
    Guid? Id,
    string ServiceCode,
    string ServiceName,
    ClientServiceAgreementUnitType? UnitType,
    int? MinutesPerUnit,
    decimal? AuthorizedUnits,
    decimal? RatePerUnit,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateOnly? RateEffectiveFrom,
    DateOnly? RateEffectiveTo,
    decimal? AuthorizedBudgetAmount,
    decimal? PlannedHoursPerVisit,
    decimal? PlannedVisitsPerWeek,
    IReadOnlyList<SaveClientServiceAgreementLineDiagnosis> Diagnoses);

public sealed record SaveClientServiceAgreementLineDiagnosis(
    Guid? Id,
    string? DiagnosisText,
    string? IcdCode,
    bool IsPrimary);

public enum ClientServiceAgreementMutationStatus
{
    Succeeded,
    ClientNotFound,
    NotFound,
    Conflict,
    InvalidPayer,
    InvalidCoverage,
}

public sealed record ClientServiceAgreementMutation(
    ClientServiceAgreementMutationStatus Status,
    ClientServiceAgreementItem? Item = null);
