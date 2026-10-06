namespace CareComply.Api.Contracts.Clients;

public sealed record SaveClientHospitalizationRequest(
    string HospitalName,
    DateTimeOffset AdmittedAtUtc,
    DateTimeOffset? DischargedAtUtc,
    string Reason,
    string? Diagnosis,
    string? DischargeInstructions,
    string? EditorVersion
);

public sealed record ClientHospitalizationResponse(
    Guid Id,
    string HospitalName,
    DateTimeOffset AdmittedAtUtc,
    DateTimeOffset? DischargedAtUtc,
    string Reason,
    string Diagnosis,
    string DischargeInstructions,
    string Status,
    string EditorVersion
);
