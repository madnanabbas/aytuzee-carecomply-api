using CareComply.Application.Abstractions.AccessManagement;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Queries.GetRole;

public sealed class GetManagedRoleQueryHandler(IRoleManagement roles)
    : IQueryHandler<GetManagedRoleQuery, GetManagedRoleResult>
{
    public async ValueTask<GetManagedRoleResult> Handle(
        GetManagedRoleQuery query,
        CancellationToken cancellationToken) =>
        GetManagedRoleResult.Succeeded(await roles.GetAsync(query.RoleId, cancellationToken));
}
