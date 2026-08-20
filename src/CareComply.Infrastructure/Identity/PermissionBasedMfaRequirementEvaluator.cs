using CareComply.Application.Abstractions.Authorization;
using CareComply.Application.Abstractions.Identity;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.Identity;

internal sealed class PermissionBasedMfaRequirementEvaluator(
    IUserPermissionResolver permissionResolver,
    IOptions<MfaPolicyOptions> options)
    : IMfaRequirementEvaluator
{
    public async Task<bool> IsRequiredAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var permissions = await permissionResolver.ResolveAsync(userId, cancellationToken);
        return options.Value.RequiredPermissions.Any(permissions.Contains);
    }
}
