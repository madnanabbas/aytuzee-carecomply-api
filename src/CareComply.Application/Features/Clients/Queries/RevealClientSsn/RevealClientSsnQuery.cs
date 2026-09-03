using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.Clients.Queries.RevealClientSsn;

public sealed record RevealClientSsnQuery(Guid ClientId)
    : IQuery<RevealClientSsnResult>, IRequirePermission, IAuditableRequest<RevealClientSsnResult>
{
    public string RequiredPermission => Permissions.Client.RevealSsn;

    public AuditEvent CreateAuditEvent(RevealClientSsnResult response) => new(
        "Client.SsnRevealed",
        "Client",
        ClientId.ToString("N"),
        response.Ssn is null ? AuditOutcome.Failed : AuditOutcome.Succeeded,
        AuditMetadata.Create(("reasonCode", response.Ssn is null ? "not-found" : "revealed")));
}
