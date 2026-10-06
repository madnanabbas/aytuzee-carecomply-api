using Microsoft.AspNetCore.Identity;

namespace CareComply.Infrastructure.Identity.Development;

internal static class DevelopmentIdentityResultGuard
{
    public static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errorCodes = string.Join(
            ", ",
            result.Errors
                .Select(error => error.Code)
                .Order(StringComparer.Ordinal));
        throw new InvalidOperationException(
            $"Identity could not {operation}. Error codes: {errorCodes}.");
    }
}
