using CareComply.Application.Abstractions.AccessManagement;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Queries.ListRoles;

public sealed class ListManagedRolesQueryHandler(IRoleManagement roles)
    : IQueryHandler<ListManagedRolesQuery, ListManagedRolesResult>
{
    public async ValueTask<ListManagedRolesResult> Handle(
        ListManagedRolesQuery query,
        CancellationToken cancellationToken) =>
        ListManagedRolesResult.Succeeded(await roles.ListAsync(cancellationToken));
}
