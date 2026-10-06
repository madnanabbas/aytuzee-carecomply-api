using System.Text;
using CareComply.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace CareComply.Infrastructure.Identity;

internal sealed class IdentityPasswordRecovery(UserManager<ApplicationUser> userManager)
    : IPasswordRecovery
{
    public async Task<PasswordResetChallenge?> CreateChallengeAsync(
        string email,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(email);

        if (user is null || !user.IsActive || !user.EmailConfirmed || string.IsNullOrWhiteSpace(user.Email))
        {
            return null;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        cancellationToken.ThrowIfCancellationRequested();
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        return new PasswordResetChallenge(user.Id, user.Email, encodedToken);
    }

    public async Task<PasswordResetOutcome> ResetPasswordAsync(
        Guid userId,
        string urlSafeToken,
        string newPassword,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(urlSafeToken));
        }
        catch (FormatException)
        {
            return PasswordResetOutcome.Invalid;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive || !user.EmailConfirmed)
        {
            return PasswordResetOutcome.Invalid;
        }

        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        cancellationToken.ThrowIfCancellationRequested();
        return result.Succeeded ? PasswordResetOutcome.Succeeded : PasswordResetOutcome.Invalid;
    }
}
