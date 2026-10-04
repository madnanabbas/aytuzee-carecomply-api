using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.Clients.Commands.CompleteClientCreation;

public sealed record CompleteClientCreationCommand(Guid ClientId, string EditorVersion)
    : ICommand<CompleteClientCreationResult>,
        IRequirePermission,
        IAuditableRequest<CompleteClientCreationResult>
{
    public string RequiredPermission => Permissions.Client.Edit;

    public AuditEvent CreateAuditEvent(CompleteClientCreationResult response)
    {
        var mutation = response.Administration;
        var succeeded = mutation?.Status == ClientAdministrationStatus.Succeeded;
        return new(
            "Client.StatusChanged",
            "Client",
            ClientId.ToString("N"),
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Create(
                    ("previousStatus", "Pending"),
                    ("newStatus", "Active"),
                    ("changed", "true"))
                : AuditMetadata.Create(("reasonCode", response.ReasonCode)));
    }
}

public sealed record CompleteClientCreationResult(
    ClientAdministrationResult? Administration,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<CompleteClientCreationResult>
{
    public string ReasonCode => AuthorizationFailure?.ToString()
        ?? Administration?.Status.ToString()
        ?? "Unavailable";

    public static CompleteClientCreationResult Completed(ClientAdministrationResult administration) =>
        new(administration, null);

    public static CompleteClientCreationResult Denied(AuthorizationFailure failure) =>
        new(null, failure);
}

public sealed class CompleteClientCreationCommandHandler(IClientAdministration administration)
    : ICommandHandler<CompleteClientCreationCommand, CompleteClientCreationResult>
{
    public async ValueTask<CompleteClientCreationResult> Handle(
        CompleteClientCreationCommand command,
        CancellationToken cancellationToken) =>
        CompleteClientCreationResult.Completed(
            await administration.CompleteCreationAsync(
                new(command.ClientId, command.EditorVersion),
                cancellationToken));
}
