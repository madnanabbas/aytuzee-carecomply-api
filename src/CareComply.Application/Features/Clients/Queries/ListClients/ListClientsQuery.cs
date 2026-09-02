using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Authorization;
using CareComply.Domain.Clients;
using Mediator;

namespace CareComply.Application.Features.Clients.Queries.ListClients;

public sealed record ListClientsQuery(
    string? Search,
    ClientStatus? Status,
    string? Location,
    ClientProgramType? ProgramType,
    string? ServiceCode,
    bool? IsOnboarding,
    ClientDirectorySort Sort,
    bool SortDescending,
    int PageNumber,
    int PageSize)
    : IQuery<ListClientsResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.Client.View;
}
