namespace CareComply.Api.Contracts.Clients;

public sealed record CreateClientTodoRequest(string Title, string? Description, Guid AssignedToUserId,
    DateOnly? DueDate, string Priority);
public sealed record UpdateClientTodoRequest(string Title, string? Description, Guid AssignedToUserId,
    DateOnly? DueDate, string Priority, string EditorVersion);
public sealed record ClientTodoTransitionRequest(string EditorVersion);
public sealed record ClientTodoResponse(Guid Id, string Title, string Description,
    ClientTodoUserResponse AssignedTo, DateOnly? DueDate, string Priority, string Status, bool IsOverdue,
    ClientTodoUserResponse CreatedBy, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc, string EditorVersion);
public sealed record ClientTodoUserResponse(Guid Id, string DisplayName);
