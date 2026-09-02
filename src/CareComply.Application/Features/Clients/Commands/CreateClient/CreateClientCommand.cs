using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Domain.Clients;
using Mediator;

namespace CareComply.Application.Features.Clients.Commands.CreateClient;

public sealed record CreateClientCommand(
    ClientIdentityInput Identity,
    ClientContactInput Contact,
    ClientCareSetupInput CareSetup,
    ClientSafetyInput Safety,
    ClientAdditionalProfileInput AdditionalProfile,
    ClientStatus InitialStatus,
    DateOnly? DischargeDate,
    string? DischargeReason,
    IReadOnlyList<ClientServiceInput> Services,
    IReadOnlyList<ClientLanguageInput> Languages)
    : ICommand<CreateClientResult>, IRequirePermission, IAuditableRequest<CreateClientResult>
{
    public string RequiredPermission => Permissions.Client.Create;

    public AuditEvent CreateAuditEvent(CreateClientResult response)
    {
        var succeeded = response.Administration?.Status == ClientAdministrationStatus.Succeeded;
        return new(
            "Client.Created",
            "Client",
            response.Administration?.Client?.Id.ToString("N") ?? "not-created",
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Create(
                    ("status", InitialStatus.ToString()),
                    ("serviceCount", Services.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("languageCount", Languages.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                : AuditMetadata.Create(("reasonCode", response.ReasonCode)));
    }
}
