using CareComply.Application.Abstractions.Clients;
using Mediator;

namespace CareComply.Application.Features.Clients.Queries.RevealClientSsn;

public sealed class RevealClientSsnQueryHandler(IClientSensitiveData sensitiveData)
    : IQueryHandler<RevealClientSsnQuery, RevealClientSsnResult>
{
    public async ValueTask<RevealClientSsnResult> Handle(
        RevealClientSsnQuery query,
        CancellationToken cancellationToken) =>
        RevealClientSsnResult.Succeeded(
            await sensitiveData.RevealSsnAsync(query.ClientId, cancellationToken));
}
