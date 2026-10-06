using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.Clients.Commands.UpdateClientAdditionalInformation;

public sealed record UpdateClientAdditionalInformationCommand(
    Guid ClientId,
    ClientAdditionalProfileInput AdditionalProfile,
    string EditorVersion)
    : ICommand<UpdateClientAdditionalInformationResult>, IRequirePermission,
        IAuditableRequest<UpdateClientAdditionalInformationResult>
{
    public string RequiredPermission => Permissions.Client.Edit;

    public AuditEvent CreateAuditEvent(UpdateClientAdditionalInformationResult response)
    {
        var status = response.Administration?.Status;
        var succeeded = status is ClientAdministrationStatus.Succeeded or ClientAdministrationStatus.NoChanges;
        return new(
            "Client.Updated",
            "Client",
            ClientId.ToString("N"),
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Create(
                    ("changedSectionCount", (response.Administration?.ChangedSections.Count ?? 0)
                        .ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("changedSections", string.Join(',', response.Administration?.ChangedSections ?? [])))
                : AuditMetadata.Create(("reasonCode", response.ReasonCode)));
    }
}

public sealed record UpdateClientAdditionalInformationResult(
    ClientAdministrationResult? Administration,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<UpdateClientAdditionalInformationResult>
{
    public string ReasonCode => AuthorizationFailure?.ToString()
        ?? Administration?.Status.ToString()
        ?? "Unavailable";

    public static UpdateClientAdditionalInformationResult Completed(ClientAdministrationResult administration) =>
        new(administration, null);

    public static UpdateClientAdditionalInformationResult Denied(AuthorizationFailure failure) => new(null, failure);
}

public sealed class UpdateClientAdditionalInformationCommandHandler(IClientAdministration administration)
    : ICommandHandler<UpdateClientAdditionalInformationCommand, UpdateClientAdditionalInformationResult>
{
    public async ValueTask<UpdateClientAdditionalInformationResult> Handle(
        UpdateClientAdditionalInformationCommand command,
        CancellationToken cancellationToken) =>
        UpdateClientAdditionalInformationResult.Completed(
            await administration.UpdateAdditionalInformationAsync(
                new(command.ClientId, command.AdditionalProfile, command.EditorVersion),
                cancellationToken));
}
