using CareComply.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;

namespace CareComply.Infrastructure.Identity.Development;

internal sealed class DevelopmentUserReconciler(
    TimeProvider timeProvider,
    UserManager<ApplicationUser> userManager)
{
    public async Task ReconcileAsync(
        IReadOnlyDictionary<DevelopmentPasswordKey, string> passwords,
        IReadOnlyDictionary<string, ApplicationRole> roles,
        CancellationToken cancellationToken)
    {
        foreach (var fixture in DevelopmentIdentityFixtures.Users)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ReconcileUserAsync(
                fixture,
                passwords[fixture.PasswordKey],
                roles[fixture.RoleName]);
        }
    }

    private async Task ReconcileUserAsync(
        DevelopmentUserFixture fixture,
        string password,
        ApplicationRole role)
    {
        var user = await userManager.FindByEmailAsync(fixture.Email);
        if (user is null)
        {
            var now = timeProvider.GetUtcNow();
            user = new ApplicationUser
            {
                CompanyId = TenantDefaults.DefaultCompanyId,
                Email = fixture.Email,
                UserName = fixture.Email,
                EmailConfirmed = true,
                FirstName = fixture.FirstName,
                LastName = fixture.LastName,
                IsActive = true,
                LockoutEnabled = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            DevelopmentIdentityResultGuard.EnsureSucceeded(
                await userManager.CreateAsync(user, password),
                $"create the development user '{fixture.Email}'");
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles
            .Where(currentRole => !string.Equals(
                currentRole,
                role.Name,
                StringComparison.Ordinal))
            .ToArray();
        if (rolesToRemove.Length > 0)
        {
            DevelopmentIdentityResultGuard.EnsureSucceeded(
                await userManager.RemoveFromRolesAsync(user, rolesToRemove),
                $"synchronize roles for the development user '{fixture.Email}'");
        }

        if (!currentRoles.Contains(role.Name!, StringComparer.Ordinal))
        {
            DevelopmentIdentityResultGuard.EnsureSucceeded(
                await userManager.AddToRoleAsync(user, role.Name!),
                $"assign the development role for '{fixture.Email}'");
        }
    }
}
