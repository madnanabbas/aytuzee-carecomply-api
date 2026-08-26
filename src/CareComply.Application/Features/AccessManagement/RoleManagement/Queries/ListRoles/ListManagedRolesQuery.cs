using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Queries.ListRoles;

public sealed record ListManagedRolesQuery : IQuery<ListManagedRolesResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.Role.View;
}
