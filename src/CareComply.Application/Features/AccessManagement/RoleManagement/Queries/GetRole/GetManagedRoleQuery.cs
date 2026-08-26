using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Queries.GetRole;

public sealed record GetManagedRoleQuery(Guid RoleId) : IQuery<GetManagedRoleResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.Role.View;
}
