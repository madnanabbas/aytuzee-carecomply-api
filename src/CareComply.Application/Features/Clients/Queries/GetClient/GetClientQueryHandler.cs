using CareComply.Application.Abstractions.Clients;
using Mediator;

namespace CareComply.Application.Features.Clients.Queries.GetClient;

public sealed class GetClientQueryHandler(IClientDirectory directory)
    : IQueryHandler<GetClientQuery, GetClientResult>
{
    public async ValueTask<GetClientResult> Handle(
        GetClientQuery query,
        CancellationToken cancellationToken) =>
        GetClientResult.Succeeded(await directory.GetByIdAsync(query.ClientId, cancellationToken));
}
