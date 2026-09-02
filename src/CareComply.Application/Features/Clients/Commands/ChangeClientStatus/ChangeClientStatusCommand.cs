using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Domain.Clients;
using Mediator;

namespace CareComply.Application.Features.Clients.Commands.ChangeClientStatus;

public sealed record ChangeClientStatusCommand(
    Guid ClientId,
    ClientStatus Status,
    DateOnly? DischargeDate,
    string? DischargeReason,
    string EditorVersion)
    : ICommand<ChangeClientStatusResult>, IRequirePermission, IAuditableRequest<ChangeClientStatusResult>
{
    public string RequiredPermission => Permissions.Client.Edit;

    public AuditEvent CreateAuditEvent(ChangeClientStatusResult response)
    {
        var mutation = response.Administration;
        var succeeded = mutation?.Status is ClientAdministrationStatus.Succeeded or ClientAdministrationStatus.NoChanges;
        return new(
            "Client.StatusChanged",
            "Client",
            ClientId.ToString("N"),
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Create(
                    ("previousStatus", mutation?.PreviousStatus?.ToString() ?? Status.ToString()),
                    ("newStatus", Status.ToString()),
                    ("changed", mutation?.Status == ClientAdministrationStatus.Succeeded ? "true" : "false"))
                : AuditMetadata.Create(("reasonCode", response.ReasonCode)));
    }
}
