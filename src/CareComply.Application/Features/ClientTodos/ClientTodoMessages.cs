using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Domain.Clients;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Features.ClientTodos;

public sealed record ListClientTodosQuery(Guid ClientId)
    : IQuery<ListClientTodosResult>,
        IRequirePermission
{
    public string RequiredPermission => Permissions.ClientTodo.View;
}

public sealed record ListClientTodoAssigneesQuery
    : IQuery<ListClientTodoAssigneesResult>,
        IRequirePermission
{
    public string RequiredPermission => Permissions.ClientTodo.View;
}

public sealed record CreateClientTodoCommand(
    Guid ClientId,
    string Title,
    string? Description,
    Guid AssignedToUserId,
    DateOnly? DueDate,
    ClientTodoPriority Priority
)
    : ICommand<ClientTodoOperationResult>,
        IRequirePermission,
        IAuditableRequest<ClientTodoOperationResult>
{
    public string RequiredPermission => Permissions.ClientTodo.Create;

    public AuditEvent CreateAuditEvent(ClientTodoOperationResult response) =>
        TodoAudit("ClientTodo.Created", response);

    private AuditEvent TodoAudit(string action, ClientTodoOperationResult response) =>
        ClientTodoAudit.Create(action, ClientId, response);
}

public sealed record UpdateClientTodoCommand(
    Guid ClientId,
    Guid TodoId,
    string Title,
    string? Description,
    Guid AssignedToUserId,
    DateOnly? DueDate,
    ClientTodoPriority Priority,
    string EditorVersion
)
    : ICommand<ClientTodoOperationResult>,
        IRequirePermission,
        IAuditableRequest<ClientTodoOperationResult>
{
    public string RequiredPermission => Permissions.ClientTodo.Edit;

    public AuditEvent CreateAuditEvent(ClientTodoOperationResult response) =>
        ClientTodoAudit.Create("ClientTodo.Updated", ClientId, response, TodoId);
}

public abstract record ClientTodoTransitionCommand(Guid ClientId, Guid TodoId, string EditorVersion)
    : ICommand<ClientTodoOperationResult>,
        IRequirePermission,
        IAuditableRequest<ClientTodoOperationResult>
{
    public abstract string RequiredPermission { get; }
    protected abstract string AuditAction { get; }

    public AuditEvent CreateAuditEvent(ClientTodoOperationResult response) =>
        ClientTodoAudit.Create(AuditAction, ClientId, response, TodoId);
}

public sealed record CompleteClientTodoCommand(Guid ClientId, Guid TodoId, string EditorVersion)
    : ClientTodoTransitionCommand(ClientId, TodoId, EditorVersion)
{
    public override string RequiredPermission => Permissions.ClientTodo.Complete;
    protected override string AuditAction => "ClientTodo.Completed";
}

public sealed record ReopenClientTodoCommand(Guid ClientId, Guid TodoId, string EditorVersion)
    : ClientTodoTransitionCommand(ClientId, TodoId, EditorVersion)
{
    public override string RequiredPermission => Permissions.ClientTodo.Reopen;
    protected override string AuditAction => "ClientTodo.Reopened";
}

public sealed record CancelClientTodoCommand(Guid ClientId, Guid TodoId, string EditorVersion)
    : ClientTodoTransitionCommand(ClientId, TodoId, EditorVersion)
{
    public override string RequiredPermission => Permissions.ClientTodo.Cancel;
    protected override string AuditAction => "ClientTodo.Cancelled";
}

public sealed record ListClientTodosResult(
    ClientTodoList? Todos,
    AuthorizationFailure? AuthorizationFailure
)
    : IAuthorizationFailureResponse<ListClientTodosResult>
{
    public static ListClientTodosResult Completed(ClientTodoList? todos) => new(todos, null);

    public static ListClientTodosResult Denied(AuthorizationFailure failure) => new(null, failure);
}

public sealed record ListClientTodoAssigneesResult(
    IReadOnlyList<ClientTodoAssignee>? Assignees,
    AuthorizationFailure? AuthorizationFailure
)
    : IAuthorizationFailureResponse<ListClientTodoAssigneesResult>
{
    public static ListClientTodoAssigneesResult Completed(
        IReadOnlyList<ClientTodoAssignee> values
    ) => new(values, null);

    public static ListClientTodoAssigneesResult Denied(AuthorizationFailure failure) =>
        new(null, failure);
}

public sealed record ClientTodoOperationResult(
    ClientTodoMutationResult? Mutation,
    AuthorizationFailure? AuthorizationFailure
)
    : IAuthorizationFailureResponse<ClientTodoOperationResult>
{
    public string ReasonCode =>
        AuthorizationFailure?.ToString() ?? Mutation?.Status.ToString() ?? "Unavailable";

    public static ClientTodoOperationResult Completed(ClientTodoMutationResult result) =>
        new(result, null);

    public static ClientTodoOperationResult Denied(AuthorizationFailure failure) =>
        new(null, failure);
}




public sealed class ListClientTodosQueryHandler(IClientTodoStore store, TimeProvider timeProvider)
    : IQueryHandler<ListClientTodosQuery, ListClientTodosResult>
{
    public async ValueTask<ListClientTodosResult> Handle(
        ListClientTodosQuery query,
        CancellationToken cancellationToken
    ) =>
        ListClientTodosResult.Completed(
            await store.ListForClientAsync(query.ClientId, Today(timeProvider), cancellationToken)
        );

    private static DateOnly Today(TimeProvider provider) =>
        DateOnly.FromDateTime(provider.GetUtcNow().UtcDateTime);
}

public sealed class ListClientTodoAssigneesQueryHandler(IClientTodoStore store)
    : IQueryHandler<ListClientTodoAssigneesQuery, ListClientTodoAssigneesResult>
{
    public async ValueTask<ListClientTodoAssigneesResult> Handle(
        ListClientTodoAssigneesQuery query,
        CancellationToken cancellationToken
    ) =>
        ListClientTodoAssigneesResult.Completed(
            await store.ListActiveAssigneesAsync(cancellationToken)
        );
}

public sealed class CreateClientTodoCommandHandler(
    IClientTodoStore store,
    ICurrentActor actor,
    TimeProvider timeProvider
) : ICommandHandler<CreateClientTodoCommand, ClientTodoOperationResult>
{
    public async ValueTask<ClientTodoOperationResult> Handle(
        CreateClientTodoCommand command,
        CancellationToken cancellationToken
    )
    {
        var now = timeProvider.GetUtcNow();
        return ClientTodoOperationResult.Completed(
            await store.CreateAsync(
                new(
                    command.ClientId,
                    command.Title,
                    command.Description,
                    command.AssignedToUserId,
                    command.DueDate,
                    command.Priority,
                    actor.UserId!.Value,
                    now
                ),
                cancellationToken
            )
        );
    }
}

public sealed class UpdateClientTodoCommandHandler(
    IClientTodoStore store,
    TimeProvider timeProvider
) : ICommandHandler<UpdateClientTodoCommand, ClientTodoOperationResult>
{
    public async ValueTask<ClientTodoOperationResult> Handle(
        UpdateClientTodoCommand command,
        CancellationToken cancellationToken
    )
    {
        var now = timeProvider.GetUtcNow();
        return ClientTodoOperationResult.Completed(
            await store.UpdateAsync(
                new(
                    command.ClientId,
                    command.TodoId,
                    command.Title,
                    command.Description,
                    command.AssignedToUserId,
                    command.DueDate,
                    command.Priority,
                    command.EditorVersion,
                    DateOnly.FromDateTime(now.UtcDateTime),
                    now
                ),
                cancellationToken
            )
        );
    }
}

public sealed class ClientTodoTransitionCommandHandler(
    IClientTodoStore store,
    ICurrentActor actor,
    TimeProvider timeProvider
)
    : ICommandHandler<CompleteClientTodoCommand, ClientTodoOperationResult>,
        ICommandHandler<ReopenClientTodoCommand, ClientTodoOperationResult>,
        ICommandHandler<CancelClientTodoCommand, ClientTodoOperationResult>
{
    public ValueTask<ClientTodoOperationResult> Handle(
        CompleteClientTodoCommand command,
        CancellationToken cancellationToken
    ) => Execute(command, ClientTodoTransition.Complete, cancellationToken);

    public ValueTask<ClientTodoOperationResult> Handle(
        ReopenClientTodoCommand command,
        CancellationToken cancellationToken
    ) => Execute(command, ClientTodoTransition.Reopen, cancellationToken);

    public ValueTask<ClientTodoOperationResult> Handle(
        CancelClientTodoCommand command,
        CancellationToken cancellationToken
    ) => Execute(command, ClientTodoTransition.Cancel, cancellationToken);

    private async ValueTask<ClientTodoOperationResult> Execute(
        ClientTodoTransitionCommand command,
        ClientTodoTransition transition,
        CancellationToken ct
    )
    {
        var now = timeProvider.GetUtcNow();
        return ClientTodoOperationResult.Completed(
            await store.TransitionAsync(
                new(
                    command.ClientId,
                    command.TodoId,
                    transition,
                    actor.UserId!.Value,
                    command.EditorVersion,
                    DateOnly.FromDateTime(now.UtcDateTime),
                    now
                ),
                ct
            )
        );
    }
}

public sealed class ListClientTodosQueryValidator : AbstractValidator<ListClientTodosQuery>
{
    public ListClientTodosQueryValidator() => RuleFor(x => x.ClientId).NotEmpty();
}

public sealed class CreateClientTodoCommandValidator : AbstractValidator<CreateClientTodoCommand>
{
    public CreateClientTodoCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(ClientTodo.MaximumTitleLength);
        RuleFor(x => x.Description).MaximumLength(ClientTodo.MaximumDescriptionLength);
        RuleFor(x => x.AssignedToUserId).NotEmpty();
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public sealed class UpdateClientTodoCommandValidator : AbstractValidator<UpdateClientTodoCommand>
{
    public UpdateClientTodoCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.TodoId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(ClientTodo.MaximumTitleLength);
        RuleFor(x => x.Description).MaximumLength(ClientTodo.MaximumDescriptionLength);
        RuleFor(x => x.AssignedToUserId).NotEmpty();
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.EditorVersion).NotEmpty().Length(64);
    }
}

public abstract class ClientTodoTransitionCommandValidator<T> : AbstractValidator<T>
    where T : ClientTodoTransitionCommand
{
    protected ClientTodoTransitionCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.TodoId).NotEmpty();
        RuleFor(x => x.EditorVersion).NotEmpty().Length(64);
    }
}

public sealed class CompleteClientTodoCommandValidator
    : ClientTodoTransitionCommandValidator<CompleteClientTodoCommand> { }

public sealed class ReopenClientTodoCommandValidator
    : ClientTodoTransitionCommandValidator<ReopenClientTodoCommand> { }

public sealed class CancelClientTodoCommandValidator
    : ClientTodoTransitionCommandValidator<CancelClientTodoCommand> { }

internal static class ClientTodoAudit
{
    public static AuditEvent Create(
        string action,
        Guid clientId,
        ClientTodoOperationResult response,
        Guid? requestedTodoId = null
    )
    {
        var mutation = response.Mutation;
        var succeeded = mutation?.Status == ClientTodoMutationStatus.Succeeded;
        return new(
            action,
            "ClientTodo",
            (mutation?.Todo?.Id ?? requestedTodoId ?? clientId).ToString("N"),
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Create(("clientId", clientId.ToString("N")))
                : AuditMetadata.Create(("reasonCode", response.ReasonCode))
        );
    }
}
