using CareComply.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Identity;

namespace CareComply.Infrastructure.Identity;

internal static class IdentityUserFailureClassifier
{
    public static UserAdministrationStatus ClassifyProfileUpdate(IdentityResult result)
    {
        if (result.Errors.Any(error => string.Equals(
                error.Code,
                nameof(IdentityErrorDescriber.ConcurrencyFailure),
                StringComparison.Ordinal)))
        {
            return UserAdministrationStatus.Conflict;
        }

        return result.Errors.Any(error =>
            error.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase))
                ? UserAdministrationStatus.DuplicateEmail
                : UserAdministrationStatus.Conflict;
    }

    public static UserAdministrationStatus ClassifyPasswordReset(IdentityResult result) =>
        result.Errors.Any(error => error.Code.StartsWith("Password", StringComparison.Ordinal))
            ? UserAdministrationStatus.InvalidPassword
            : UserAdministrationStatus.Conflict;
}
