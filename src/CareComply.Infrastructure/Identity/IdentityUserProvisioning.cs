using CareComply.Application.Abstractions.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Identity;

internal sealed class IdentityUserProvisioning(
    CareComplyDbContext context,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : IUserProvisioning
{
    public async Task<UserProvisioningResult> CreateAsync(
        UserProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        var roles = await context.Roles
            .Where(role => request.RoleIds.Contains(role.Id))
            .OrderBy(role => role.Name)
            .Select(role => new RoleProjection(role.Id, role.Name ?? string.Empty))
            .ToArrayAsync(cancellationToken);

        if (roles.Length != request.RoleIds.Count
            || roles.Any(role => string.IsNullOrWhiteSpace(role.Name)))
        {
            return new(UserProvisioningStatus.RoleNotFound, null);
        }

        var now = timeProvider.GetUtcNow();
        var user = new ApplicationUser
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            UserName = request.Email,
            PhoneNumber = request.PhoneNumber,
            IsActive = request.IsActive,
            RequiresMfa = request.RequiresMfa,
            MfaMethod = request.MfaMethod,
            EmailConfirmed = true,
            LockoutEnabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        cancellationToken.ThrowIfCancellationRequested();
        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return new(MapCreateFailure(createResult), null);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var assignmentResult = await userManager.AddToRolesAsync(
            user,
            roles.Select(role => role.Name));
        if (!assignmentResult.Succeeded)
        {
            var roleWasRemoved = assignmentResult.Errors.Any(error =>
                string.Equals(error.Code, "RoleNotFound", StringComparison.Ordinal));
            return new(
                roleWasRemoved
                    ? UserProvisioningStatus.RoleNotFound
                    : UserProvisioningStatus.RoleAssignmentFailed,
                null);
        }

        return new(UserProvisioningStatus.Succeeded, user.Id);
    }

    private static UserProvisioningStatus MapCreateFailure(IdentityResult result)
    {
        if (result.Errors.Any(error =>
                error.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)))
        {
            return UserProvisioningStatus.DuplicateEmail;
        }

        return result.Errors.Any(error =>
            error.Code.StartsWith("Password", StringComparison.Ordinal))
            ? UserProvisioningStatus.InvalidPassword
            : UserProvisioningStatus.Unavailable;
    }

    private sealed record RoleProjection(Guid Id, string Name);
}
