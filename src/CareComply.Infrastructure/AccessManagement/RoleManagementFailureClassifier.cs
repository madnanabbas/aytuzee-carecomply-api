using CareComply.Application.Abstractions.AccessManagement;
using Microsoft.AspNetCore.Identity;

namespace CareComply.Infrastructure.AccessManagement;

internal static class RoleManagementFailureClassifier
{
    public static RoleMutationStatus ClassifyCreate(IdentityResult result) =>
        HasDuplicateError(result)
            ? RoleMutationStatus.DuplicateName
            : RoleMutationStatus.Unavailable;

    public static RoleMutationStatus ClassifyUpdate(IdentityResult result) =>
        HasDuplicateError(result)
            ? RoleMutationStatus.DuplicateName
            : RoleMutationStatus.Conflict;

    private static bool HasDuplicateError(IdentityResult result) =>
        result.Errors.Any(error =>
            error.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
}
