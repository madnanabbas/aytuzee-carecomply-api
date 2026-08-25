using CareComply.Application.Abstractions.Authorization;
using CareComply.Application.Abstractions.Identity;
using Microsoft.Extensions.Options;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Identity;

internal sealed class PermissionBasedMfaRequirementEvaluator(
    CareComplyDbContext context,
    IUserPermissionResolver permissionResolver,
    IOptions<MfaPolicyOptions> options)
    : IMfaRequirementEvaluator
{
    public async Task<bool> IsRequiredAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var explicitlyRequired = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.RequiresMfa)
            .SingleOrDefaultAsync(cancellationToken);
        return explicitlyRequired
            || await IsRequiredByPermissionsAsync(userId, cancellationToken);
    }

    public async Task<bool> IsRequiredByPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var permissions = await permissionResolver.ResolveAsync(userId, cancellationToken);
        return options.Value.RequiredPermissions.Any(permissions.Contains);
    }
}
