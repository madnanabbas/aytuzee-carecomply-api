using CareComply.Application.Abstractions.Clients;
using Mediator;

namespace CareComply.Application.Features.Clients.Queries.ListClients;

public sealed class ListClientsQueryHandler(IClientDirectory directory)
    : IQueryHandler<ListClientsQuery, ListClientsResult>
{
    public async ValueTask<ListClientsResult> Handle(
        ListClientsQuery query,
        CancellationToken cancellationToken) =>
        ListClientsResult.Succeeded(await directory.ListAsync(
            new(
                Normalize(query.Search),
                query.Status,
                Normalize(query.Location),
                query.ProgramType,
                Normalize(query.ServiceCode),
                query.IsOnboarding,
                query.Sort,
                query.SortDescending,
                query.PageNumber,
                query.PageSize),
            cancellationToken));

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
