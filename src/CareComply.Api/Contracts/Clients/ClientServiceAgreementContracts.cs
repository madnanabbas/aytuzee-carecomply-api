namespace CareComply.Api.Contracts.Clients;

public sealed record CreateClientServiceAgreementRequest(
    string? AgreementNumber,
    string? PayerName,
    string? InsuredMemberId,
    string? PriorAuthorizationNumber,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Status,
    string? Notes,
    IReadOnlyList<CreateClientServiceAgreementLineRequest>? Lines);

public sealed record UpdateClientServiceAgreementRequest(
    string? AgreementNumber,
    string? PayerName,
    string? InsuredMemberId,
    string? PriorAuthorizationNumber,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Status,
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
    decimal? PlannedHoursPerVisit,
    decimal? PlannedVisitsPerWeek,
    string? PrimaryDiagnosisText,
    string? IcdCode);

public sealed record UpdateClientServiceAgreementLineRequest(
    Guid? Id,
    string? ServiceCode,
    string? ServiceName,
    string? UnitType,
    int? MinutesPerUnit,
    decimal? AuthorizedUnits,
    decimal? RatePerUnit,
    decimal? PlannedHoursPerVisit,
    decimal? PlannedVisitsPerWeek,
    string? PrimaryDiagnosisText,
    string? IcdCode);

public sealed record ClientServiceAgreementResponse(
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
    IReadOnlyList<ClientServiceAgreementLineResponse> Lines);

public sealed record ClientServiceAgreementLineResponse(
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
