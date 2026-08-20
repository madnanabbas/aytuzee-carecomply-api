using CareComply.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Identity;

namespace CareComply.Infrastructure.Identity;

internal sealed class IdentityUserReader(UserManager<ApplicationUser> userManager)
    : IIdentityUserReader
{
    public async Task<IdentityUserSnapshot?> FindByIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is not { IsActive: true } || string.IsNullOrWhiteSpace(user.Email))
        {
            return null;
        }

        return new IdentityUserSnapshot(
            user.Id,
            user.Email,
            string.Join(' ', user.FirstName, user.LastName).Trim());
    }
}
