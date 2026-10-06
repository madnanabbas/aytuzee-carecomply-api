using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.Roles.Queries.ListRoles;

public sealed class ListRolesQueryHandler(IRoleDirectory roleDirectory)
    : IQueryHandler<ListRolesQuery, ListRolesResult>
{
    public async ValueTask<ListRolesResult> Handle(
        ListRolesQuery query,
        CancellationToken cancellationToken) =>
        ListRolesResult.Succeeded(await roleDirectory.ListAsync(cancellationToken));
}
