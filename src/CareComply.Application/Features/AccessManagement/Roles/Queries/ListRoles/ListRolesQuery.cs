using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.Roles.Queries.ListRoles;

public sealed record ListRolesQuery : IQuery<ListRolesResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.User.Manage;
}
