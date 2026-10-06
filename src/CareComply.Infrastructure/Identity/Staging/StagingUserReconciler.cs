using CareComply.Infrastructure.Persistence;
using CareComply.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Identity.Staging;

internal sealed class StagingUserReconciler(
    CareComplyDbContext context,
    TimeProvider timeProvider,
    UserManager<ApplicationUser> userManager)
{
    public async Task ReconcileAsync(
        IReadOnlyDictionary<StagingPasswordKey, string> passwords,
        IReadOnlyDictionary<string, ApplicationRole> roles,
        CancellationToken cancellationToken)
    {
        foreach (var fixture in StagingIdentityFixtures.Users)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ReconcileUserAsync(
                fixture,
                passwords[fixture.PasswordKey],
                roles[fixture.RoleName]);
        }
    }

    private async Task ReconcileUserAsync(
        StagingUserFixture fixture,
        string password,
        ApplicationRole role)
    {
        var user = await userManager.FindByEmailAsync(fixture.Email);
        if (user is null)
        {
            var now = timeProvider.GetUtcNow();
            user = new ApplicationUser
            {
                CompanyId = fixture.RoleName == "System Administrator"
                    ? null
                    : TenantDefaults.DefaultCompanyId,
                IsPlatformAdministrator = fixture.RoleName == "System Administrator",
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

            StagingIdentityResultGuard.EnsureSucceeded(
                await userManager.CreateAsync(user, password),
                $"create the staging user '{fixture.Email}'");
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
            StagingIdentityResultGuard.EnsureSucceeded(
                await userManager.RemoveFromRolesAsync(user, rolesToRemove),
                $"synchronize roles for the staging user '{fixture.Email}'");
        }

        if (!currentRoles.Contains(role.Name!, StringComparer.Ordinal))
        {
            StagingIdentityResultGuard.EnsureSucceeded(
                await userManager.AddToRoleAsync(user, role.Name!),
                $"assign the staging role for '{fixture.Email}'");
        }

        if (!user.IsPlatformAdministrator)
        {
            var mainLocationId = await context.CompanyLocations
                .Where(location => location.CompanyId == user.CompanyId && location.IsMain)
                .Select(location => location.Id)
                .SingleAsync();
            if (!await context.UserLocations.AnyAsync(
                    location => location.UserId == user.Id
                        && location.LocationId == mainLocationId))
            {
                context.UserLocations.Add(new ApplicationUserLocation
                {
                    UserId = user.Id,
                    LocationId = mainLocationId,
                });
                await context.SaveChangesAsync();
            }
        }
    }
}
