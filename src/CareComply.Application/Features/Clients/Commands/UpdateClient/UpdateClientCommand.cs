using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.Clients.Commands.UpdateClient;

public sealed record UpdateClientCommand(
    Guid ClientId,
    ClientIdentityInput Identity,
    ClientContactInput Contact,
    ClientCareSetupInput CareSetup,
    ClientSafetyInput Safety,
    ClientAdditionalProfileInput AdditionalProfile,
    IReadOnlyList<ClientServiceInput> Services,
    IReadOnlyList<ClientLanguageInput> Languages,
    DateOnly? ServiceChangeEffectiveDate,
    string EditorVersion)
    : ICommand<UpdateClientResult>, IRequirePermission, IAuditableRequest<UpdateClientResult>
{
    public string RequiredPermission => Permissions.Client.Edit;

    public AuditEvent CreateAuditEvent(UpdateClientResult response)
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
