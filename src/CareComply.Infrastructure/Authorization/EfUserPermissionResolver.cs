using System.Collections.Frozen;
using CareComply.Application.Abstractions.Authorization;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Authorization;

internal sealed class EfUserPermissionResolver(CareComplyDbContext context)
    : IUserPermissionResolver
{
    private Guid? _cachedUserId;
    private IReadOnlySet<string>? _cachedPermissions;

    public async Task<IReadOnlySet<string>> ResolveAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (_cachedUserId == userId && _cachedPermissions is not null)
        {
            return _cachedPermissions;
        }

        var permissionNames = await (
                from user in context.Users.AsNoTracking()
                join userRole in context.Set<IdentityUserRole<Guid>>().AsNoTracking()
                    on user.Id equals userRole.UserId
                join rolePermission in context.Set<RolePermission>().AsNoTracking()
                    on userRole.RoleId equals rolePermission.RoleId
                join permission in context.Set<Permission>().AsNoTracking()
                    on rolePermission.PermissionId equals permission.Id
                where user.Id == userId && user.IsActive
                select permission.Name)
            .TagWith("CareComply.PermissionResolution")
            .Distinct()
            .OrderBy(name => name)
            .ToArrayAsync(cancellationToken);

        var resolvedPermissions = permissionNames.ToFrozenSet(StringComparer.Ordinal);
        _cachedUserId = userId;
        _cachedPermissions = resolvedPermissions;
        return resolvedPermissions;
    }
}
