namespace CareComply.Api.Contracts.Clients;

public sealed record SaveClientCaregiverRequest(Guid UserId, string Type, bool IsPreferred, string? Services,
    DateOnly StartDate, DateOnly? EndDate, string? Notes, string? EditorVersion);
public sealed record EndClientCaregiverRequest(DateOnly EndDate, string EditorVersion);
public sealed record ClientCaregiverCandidateResponse(Guid Id, string Name, string Title, string Phone, string Email);
public sealed record ClientCaregiverResponse(Guid Id, Guid UserId, string Name, string Title, string Phone, string Email,
    string Type, bool IsPreferred, string Services, DateOnly StartDate, DateOnly? EndDate, string Notes, string Status, string EditorVersion);
