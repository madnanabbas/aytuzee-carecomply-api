namespace CareComply.Api.Contracts.Clients;

public sealed record CreateClientServiceAgreementRequest(
    Guid? PayerId,
    Guid? ClientCoverageId,
    string? AgreementNumber,
    string? PayerName,
    string? InsuredMemberId,
    string? PriorAuthorizationNumber,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Status,
    string? Priority,
    bool IsDummy,
    string? Notes,
    IReadOnlyList<CreateClientServiceAgreementLineRequest>? Lines);

public sealed record UpdateClientServiceAgreementRequest(
    Guid? PayerId,
    Guid? ClientCoverageId,
    string? AgreementNumber,
    string? PayerName,
    string? InsuredMemberId,
    string? PriorAuthorizationNumber,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Status,
    string? Priority,
    bool IsDummy,
    string? Notes,
    string? EditorVersion,
    IReadOnlyList<UpdateClientServiceAgreementLineRequest>? Lines);

public sealed record CreateClientServiceAgreementLineRequest(
    string? ServiceCode,
    string? ServiceName,
    string? UnitType,
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
    IReadOnlyList<CreateClientServiceAgreementLineDiagnosisRequest>? Diagnoses);

public sealed record UpdateClientServiceAgreementLineRequest(
    Guid? Id,
    string? ServiceCode,
    string? ServiceName,
    string? UnitType,
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
    IReadOnlyList<UpdateClientServiceAgreementLineDiagnosisRequest>? Diagnoses);

public sealed record CreateClientServiceAgreementLineDiagnosisRequest(
    string? DiagnosisText,
    string? IcdCode,
    bool IsPrimary);

public sealed record UpdateClientServiceAgreementLineDiagnosisRequest(
    Guid? Id,
    string? DiagnosisText,
    string? IcdCode,
    bool IsPrimary);

public sealed record ClientServiceAgreementResponse(
    Guid Id,
    Guid? PayerId,
    Guid? ClientCoverageId,
    string? AgreementNumber,
    string? PayerName,
    string? InsuredMemberId,
    string? PriorAuthorizationNumber,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Status,
    string? Priority,
    bool IsDummy,
    string? Notes,
    string EditorVersion,
    IReadOnlyList<ClientServiceAgreementLineResponse> Lines,
    IReadOnlyList<ClientServiceAgreementWarningResponse> Warnings);

public sealed record ClientServiceAgreementWarningResponse(
    string Code,
    string ServiceCode,
    Guid ServiceLineId,
    Guid OverlappingAgreementId,
    Guid OverlappingLineId,
    DateOnly OverlapStartDate,
    DateOnly? OverlapEndDate);

public sealed record ClientServiceAgreementLineResponse(
    Guid Id,
    string ServiceCode,
    string ServiceName,
    string? UnitType,
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
    IReadOnlyList<ClientServiceAgreementLineDiagnosisResponse> Diagnoses,
    decimal? EstimatedBudget,
    decimal? PlannedHoursPerWeek,
    decimal? PlannedUnitsPerWeek);

public sealed record ClientServiceAgreementLineDiagnosisResponse(
    Guid Id,
    string? DiagnosisText,
    string? IcdCode,
    bool IsPrimary);
