using CareComply.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Identity;

namespace CareComply.Infrastructure.Identity;

internal sealed class IdentitySessionValidator(
    IMfaRequirementEvaluator mfaRequirementEvaluator,
    UserManager<ApplicationUser> userManager)
    : IIdentitySessionValidator
{
    public async Task<bool> IsValidAsync(
        Guid userId,
        string securityStamp,
        bool hasMfaAssurance,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is not { IsActive: true }
            || string.IsNullOrWhiteSpace(user.SecurityStamp)
            || !string.Equals(user.SecurityStamp, securityStamp, StringComparison.Ordinal))
        {
            return false;
        }

        var requiresMfa = user.TwoFactorEnabled
            || await mfaRequirementEvaluator.IsRequiredAsync(userId, cancellationToken);

        return !requiresMfa || hasMfaAssurance;
    }
}
