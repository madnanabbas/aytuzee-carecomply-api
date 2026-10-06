using CareComply.Domain.Clients;

namespace CareComply.Application.Abstractions.Clients;

public interface IClientTodoStore
{
    Task<ClientTodoList?> ListForClientAsync(Guid clientId, DateOnly today, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClientTodoAssignee>> ListActiveAssigneesAsync(CancellationToken cancellationToken);
    Task<ClientTodoMutationResult> CreateAsync(ClientTodoCreateRequest request, CancellationToken cancellationToken);
    Task<ClientTodoMutationResult> UpdateAsync(ClientTodoUpdateRequest request, CancellationToken cancellationToken);
    Task<ClientTodoMutationResult> TransitionAsync(ClientTodoTransitionStoreRequest request, CancellationToken cancellationToken);
}

public sealed record ClientTodoItem(Guid Id, Guid ClientId, string Title, string Description,
    Guid AssignedToUserId, string AssignedToDisplayName, DateOnly? DueDate,
    ClientTodoPriority Priority, ClientTodoStatus Status, bool IsOverdue,
    Guid CreatedByUserId, string CreatedByDisplayName, DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc, Guid? CompletedByUserId, DateTimeOffset? CompletedAtUtc,
    string EditorVersion);
public sealed record ClientTodoList(IReadOnlyList<ClientTodoItem> Items);
public sealed record ClientTodoAssignee(Guid Id, string DisplayName);

public sealed record ClientTodoCreateRequest(Guid ClientId, string Title, string? Description,
    Guid AssignedToUserId, DateOnly? DueDate, ClientTodoPriority Priority,
    Guid ActorUserId, DateTimeOffset NowUtc);
public sealed record ClientTodoUpdateRequest(Guid ClientId, Guid TodoId, string Title, string? Description,
    Guid AssignedToUserId, DateOnly? DueDate, ClientTodoPriority Priority,
    string EditorVersion, DateOnly Today, DateTimeOffset NowUtc);
public sealed record ClientTodoTransitionStoreRequest(Guid ClientId, Guid TodoId,
    ClientTodoTransition Transition, Guid ActorUserId, string EditorVersion,
    DateOnly Today, DateTimeOffset NowUtc);
public enum ClientTodoTransition { Complete, Reopen, Cancel }
public enum ClientTodoMutationStatus { Succeeded, ClientNotFound, TodoNotFound, InvalidAssignee, Conflict, InvalidState }
public sealed record ClientTodoMutationResult(ClientTodoMutationStatus Status, ClientTodoItem? Todo = null);
