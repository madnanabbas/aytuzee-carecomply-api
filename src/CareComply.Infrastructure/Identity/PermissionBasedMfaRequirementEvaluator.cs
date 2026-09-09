using CareComply.Application.Abstractions.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Identity;

internal sealed class PermissionBasedMfaRequirementEvaluator(
    CareComplyDbContext context)
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
        return explicitlyRequired;
    }

    public Task<bool> IsRequiredByPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }
}
