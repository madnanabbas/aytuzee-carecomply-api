using CareComply.Domain.Clients;

namespace CareComply.Application.Abstractions.Clients;

public interface IClientCaregiverStore
{
    Task<ClientCaregiverList?> ListAsync(Guid clientId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClientCaregiverCandidate>> CandidatesAsync(CancellationToken cancellationToken);
    Task<ClientCaregiverMutation> CreateAsync(ClientCaregiverCreate request, CancellationToken cancellationToken);
    Task<ClientCaregiverMutation> UpdateAsync(ClientCaregiverUpdate request, CancellationToken cancellationToken);
    Task<ClientCaregiverMutation> EndAsync(ClientCaregiverEnd request, CancellationToken cancellationToken);
}

public sealed record ClientCaregiverItem(Guid Id, Guid UserId, string Name, string Title, string Phone, string Email,
    ClientCaregiverType Type, bool IsPreferred, string Services, DateOnly StartDate, DateOnly? EndDate,
    string Notes, ClientCaregiverStatus Status, string EditorVersion);
public sealed record ClientCaregiverCandidate(Guid Id, string Name, string Title, string Phone, string Email);
public sealed record ClientCaregiverList(IReadOnlyList<ClientCaregiverItem> Items);
public sealed record ClientCaregiverCreate(Guid ClientId, Guid UserId, ClientCaregiverType Type, bool IsPreferred,
    string? Services, DateOnly StartDate, DateOnly? EndDate, string? Notes, Guid ActorUserId, DateTimeOffset NowUtc);
public sealed record ClientCaregiverUpdate(Guid ClientId, Guid AssignmentId, ClientCaregiverType Type, bool IsPreferred,
    string? Services, DateOnly StartDate, DateOnly? EndDate, string? Notes, string EditorVersion, DateTimeOffset NowUtc);
public sealed record ClientCaregiverEnd(Guid ClientId, Guid AssignmentId, DateOnly EndDate, string EditorVersion, DateTimeOffset NowUtc);
public enum ClientCaregiverMutationStatus { Succeeded, ClientNotFound, AssignmentNotFound, InvalidCaregiver, DuplicateActiveAssignment, PrimaryConflict, Conflict, InvalidState }
public sealed record ClientCaregiverMutation(ClientCaregiverMutationStatus Status, ClientCaregiverItem? Assignment = null);
