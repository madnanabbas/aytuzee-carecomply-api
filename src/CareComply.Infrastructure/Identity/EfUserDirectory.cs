using CareComply.Application.Abstractions.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CareComply.Infrastructure.Authorization;

namespace CareComply.Infrastructure.Identity;

internal sealed class EfUserDirectory(
    CareComplyDbContext context,
    IMfaRequirementEvaluator mfaRequirementEvaluator,
    IOptions<MfaPolicyOptions> mfaOptions) : IUserDirectory
{
    public async Task<UserDirectoryDetails?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new UserDetailsProjection(
                candidate.Id,
                candidate.FirstName,
                candidate.LastName,
                candidate.Email ?? string.Empty,
                candidate.PhoneNumber,
                candidate.IsActive,
                candidate.TwoFactorEnabled,
                candidate.RequiresMfa,
                candidate.MfaMethod,
                candidate.ConcurrencyStamp ?? string.Empty))
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var roles = await (
                from userRole in context.Set<IdentityUserRole<Guid>>().AsNoTracking()
                join role in context.Roles.AsNoTracking()
                    on userRole.RoleId equals role.Id
                where userRole.UserId == user.Id
                orderby role.Name
                select new UserDirectoryRole(role.Id, role.Name ?? string.Empty))
            .ToArrayAsync(cancellationToken);

        var isMfaRequiredByPermissions = await mfaRequirementEvaluator
            .IsRequiredByPermissionsAsync(user.Id, cancellationToken);
        return MapDetails(user, isMfaRequiredByPermissions) with { Roles = roles };
    }
    public async Task<UserDirectoryPage> ListAsync(
        UserDirectoryRequest request,
        CancellationToken cancellationToken)
    {
        var users = context.Users.AsNoTracking();
        var mfaRequiredUserIds =
            from userRole in context.Set<IdentityUserRole<Guid>>().AsNoTracking()
            join rolePermission in context.Set<RolePermission>().AsNoTracking()
                on userRole.RoleId equals rolePermission.RoleId
            join permission in context.Set<Permission>().AsNoTracking()
                on rolePermission.PermissionId equals permission.Id
            where mfaOptions.Value.RequiredPermissions.Contains(permission.Name)
            select userRole.UserId;

        if (request.Search is { } search)
        {
            users = users.Where(user =>
                user.FirstName.Contains(search)
                || user.LastName.Contains(search)
                || (user.FirstName + " " + user.LastName).Contains(search)
                || (user.Email != null && user.Email.Contains(search)));
        }

        if (request.IsActive is { } isActive)
        {
            users = users.Where(user => user.IsActive == isActive);
        }

        if (request.RoleId is { } roleId)
        {
            users = users.Where(user => context.Set<IdentityUserRole<Guid>>()
                .Any(userRole => userRole.UserId == user.Id && userRole.RoleId == roleId));
        }

        if (request.MfaStatus is { } mfaStatus)
        {
            var isEnabled = mfaStatus == UserDirectoryMfaFilter.Enabled;
            users = isEnabled
                ? users.Where(user =>
                    user.TwoFactorEnabled
                    || user.RequiresMfa
                    || mfaRequiredUserIds.Contains(user.Id))
                : users.Where(user =>
                    !user.TwoFactorEnabled
                    && !user.RequiresMfa
                    && !mfaRequiredUserIds.Contains(user.Id));
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var orderedUsers = ApplySort(users, request.Sort, request.SortDescending);
        var pageUsers = await orderedUsers
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(user => new UserProjection(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email ?? string.Empty,
                user.IsActive,
                user.TwoFactorEnabled
                    || user.RequiresMfa
                    || mfaRequiredUserIds.Contains(user.Id)))
            .ToArrayAsync(cancellationToken);

        var userIds = pageUsers.Select(user => user.Id).ToArray();
        var roleAssignments = userIds.Length == 0
            ? []
            : await (
                    from userRole in context.Set<IdentityUserRole<Guid>>().AsNoTracking()
                    join role in context.Roles.AsNoTracking()
                        on userRole.RoleId equals role.Id
                    where userIds.Contains(userRole.UserId)
                    orderby role.Name
                    select new RoleAssignmentProjection(
                        userRole.UserId,
                        role.Id,
                        role.Name ?? string.Empty))
                .ToArrayAsync(cancellationToken);

        var rolesByUser = roleAssignments
            .GroupBy(assignment => assignment.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<UserDirectoryRole>)group
                    .Select(assignment => new UserDirectoryRole(
                        assignment.RoleId,
                        assignment.RoleName))
                    .ToArray());

        var items = pageUsers
            .Select(user => new UserDirectoryItem(
                user.Id,
                string.Join(' ', user.FirstName, user.LastName).Trim(),
                user.Email,
                user.IsActive,
                user.IsMfaEnabled,
                rolesByUser.GetValueOrDefault(user.Id) ?? []))
            .ToArray();

        var availableRoles = await context.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => new UserDirectoryRole(role.Id, role.Name ?? string.Empty))
            .ToArrayAsync(cancellationToken);

        return new UserDirectoryPage(
            items,
            availableRoles,
            request.PageNumber,
            request.PageSize,
            totalCount);
    }

    private static IOrderedQueryable<ApplicationUser> ApplySort(
        IQueryable<ApplicationUser> users,
        UserDirectorySort sort,
        bool descending) =>
        (sort, descending) switch
        {
            (UserDirectorySort.Email, false) => users
                .OrderBy(user => user.Email)
                .ThenBy(user => user.Id),
            (UserDirectorySort.Email, true) => users
                .OrderByDescending(user => user.Email)
                .ThenBy(user => user.Id),
            (UserDirectorySort.Status, false) => users
                .OrderByDescending(user => user.IsActive)
                .ThenBy(user => user.LastName)
                .ThenBy(user => user.FirstName)
                .ThenBy(user => user.Id),
            (UserDirectorySort.Status, true) => users
                .OrderBy(user => user.IsActive)
                .ThenBy(user => user.LastName)
                .ThenBy(user => user.FirstName)
                .ThenBy(user => user.Id),
            (UserDirectorySort.Mfa, false) => users
                .OrderByDescending(user => user.TwoFactorEnabled)
                .ThenBy(user => user.LastName)
                .ThenBy(user => user.FirstName)
                .ThenBy(user => user.Id),
            (UserDirectorySort.Mfa, true) => users
                .OrderBy(user => user.TwoFactorEnabled)
                .ThenBy(user => user.LastName)
                .ThenBy(user => user.FirstName)
                .ThenBy(user => user.Id),
            (_, false) => users
                .OrderBy(user => user.LastName)
                .ThenBy(user => user.FirstName)
                .ThenBy(user => user.Id),
            _ => users
                .OrderByDescending(user => user.LastName)
                .ThenByDescending(user => user.FirstName)
                .ThenBy(user => user.Id),
        };

    private sealed record UserProjection(
        Guid Id,
        string FirstName,
        string LastName,
        string Email,
        bool IsActive,
        bool IsMfaEnabled);

    private sealed record RoleAssignmentProjection(
        Guid UserId,
        Guid RoleId,
        string RoleName);

    private sealed record UserDetailsProjection(
        Guid Id,
        string FirstName,
        string LastName,
        string Email,
        string? PhoneNumber,
        bool IsActive,
        bool IsMfaEnabled,
        bool RequiresMfa,
        UserMfaMethod MfaMethod,
        string ConcurrencyStamp);

    private static UserDirectoryDetails MapDetails(
        UserDetailsProjection user,
        bool isMfaRequiredByPermissions) => new(
            user.Id,
            user.FirstName,
            user.LastName,
            string.Join(' ', user.FirstName, user.LastName).Trim(),
            user.Email,
            user.PhoneNumber,
            user.IsActive,
            user.IsMfaEnabled,
            user.RequiresMfa,
            user.RequiresMfa || isMfaRequiredByPermissions,
            isMfaRequiredByPermissions,
            user.MfaMethod,
            IdentityEditorVersion.Create(user.ConcurrencyStamp));
}
