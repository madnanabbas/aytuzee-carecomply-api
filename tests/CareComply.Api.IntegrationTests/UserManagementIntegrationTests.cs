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
    private readonly object _syncRoot = new();

    private Guid? _cachedUserId;
    private IReadOnlySet<string>? _cachedPermissions;

    private Guid? _inFlightUserId;
    private Task<IReadOnlySet<string>>? _inFlightResolution;

    public Task<IReadOnlySet<string>> ResolveAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (_cachedUserId == userId &&
            _cachedPermissions is not null)
        {
            return Task.FromResult(_cachedPermissions);
        }

        lock (_syncRoot)
        {
            if (_cachedUserId == userId &&
                _cachedPermissions is not null)
            {
                return Task.FromResult(_cachedPermissions);
            }

            if (_inFlightUserId == userId &&
                _inFlightResolution is not null)
            {
                return _inFlightResolution;
            }

            _inFlightUserId = userId;
            _inFlightResolution =
                ResolveAndCacheAsync(userId, cancellationToken);

            return _inFlightResolution;
        }
    }

    private async Task<IReadOnlySet<string>> ResolveAndCacheAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var permissionNames = await (
                    from user in context.Users.AsNoTracking()
                    join userRole in context
                        .Set<IdentityUserRole<Guid>>()
                        .AsNoTracking()
                        on user.Id equals userRole.UserId
                    join rolePermission in context
                        .Set<RolePermission>()
                        .AsNoTracking()
                        on userRole.RoleId equals rolePermission.RoleId
                    join permission in context
                        .Set<Permission>()
                        .AsNoTracking()
                        on rolePermission.PermissionId equals permission.Id
                    where user.Id == userId &&
                          user.IsActive
                    select permission.Name)
                .TagWith("CareComply.PermissionResolution")
                .Distinct()
                .OrderBy(name => name)
                .ToArrayAsync(cancellationToken);

            IReadOnlySet<string> resolvedPermissions =
                permissionNames.ToFrozenSet(
                    StringComparer.Ordinal);

            lock (_syncRoot)
            {
                _cachedUserId = userId;
                _cachedPermissions = resolvedPermissions;

                _inFlightUserId = null;
                _inFlightResolution = null;
            }

            return resolvedPermissions;
        }
        catch
        {
            lock (_syncRoot)
            {
                if (_inFlightUserId == userId)
                {
                    _inFlightUserId = null;
                    _inFlightResolution = null;
                }
            }

            throw;
        }
    }
}