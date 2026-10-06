using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Features.Clients.CreationProgress;

public sealed record GetClientCreationStateQuery(Guid ClientId)
    : IQuery<ClientCreationStateOperationResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.Client.Edit;
}

public sealed record AdvanceClientCreationProgressCommand(
    Guid ClientId,
    int Step,
    string EditorVersion)
    : ICommand<ClientCreationProgressOperationResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.Client.Edit;
}

public sealed record ClientCreationStateOperationResult(
    ClientCreationStateResult? Result,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<ClientCreationStateOperationResult>
{
    public static ClientCreationStateOperationResult Completed(ClientCreationStateResult result) =>
        new(result, null);

    public static ClientCreationStateOperationResult Denied(AuthorizationFailure failure) =>
        new(null, failure);
}

public sealed record ClientCreationProgressOperationResult(
    ClientCreationProgressResult? Result,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<ClientCreationProgressOperationResult>
{
    public static ClientCreationProgressOperationResult Completed(ClientCreationProgressResult result) =>
        new(result, null);

    public static ClientCreationProgressOperationResult Denied(AuthorizationFailure failure) =>
        new(null, failure);
}

public sealed class GetClientCreationStateHandler(IClientAdministration administration)
    : IQueryHandler<GetClientCreationStateQuery, ClientCreationStateOperationResult>
{
    public async ValueTask<ClientCreationStateOperationResult> Handle(
        GetClientCreationStateQuery query,
        CancellationToken cancellationToken) =>
        ClientCreationStateOperationResult.Completed(
            await administration.GetCreationStateAsync(query.ClientId, cancellationToken));
}

public sealed class AdvanceClientCreationProgressHandler(IClientAdministration administration)
    : ICommandHandler<AdvanceClientCreationProgressCommand, ClientCreationProgressOperationResult>
{
    public async ValueTask<ClientCreationProgressOperationResult> Handle(
        AdvanceClientCreationProgressCommand command,
        CancellationToken cancellationToken) =>
        ClientCreationProgressOperationResult.Completed(
            await administration.AdvanceCreationProgressAsync(
                new(command.ClientId, command.Step, command.EditorVersion),
                cancellationToken));
}

public sealed class AdvanceClientCreationProgressValidator
    : AbstractValidator<AdvanceClientCreationProgressCommand>
{
    public AdvanceClientCreationProgressValidator()
    {
        RuleFor(command => command.ClientId).NotEmpty();
        RuleFor(command => command.Step).InclusiveBetween(2, 10);
        RuleFor(command => command.EditorVersion)
            .Must(ClientValidation.IsValidEditorVersion)
            .WithMessage("Editor version is invalid.");
    }
}
