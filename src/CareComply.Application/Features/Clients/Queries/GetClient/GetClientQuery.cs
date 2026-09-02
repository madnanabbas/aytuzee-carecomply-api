using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.Clients.Queries.GetClient;

public sealed record GetClientQuery(Guid ClientId) : IQuery<GetClientResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.Client.View;
}
