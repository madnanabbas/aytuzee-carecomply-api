using CareComply.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Identity;
using CareComply.Infrastructure.Tenancy;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Identity;

internal sealed class IdentityUserGeneralAdministration(
    CareComplyDbContext context,
    ITenantScopeAccessor tenantScopeAccessor,
    UserManager<ApplicationUser> userManager,
    TenantUserAccess tenantUserAccess,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IUserGeneralAdministration
{
    public async Task<UserGeneralUpdateResult> UpdateAsync(
        UserGeneralUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!await tenantUserAccess.CanManageAsync(request.UserId, cancellationToken))
        {
            return new(UserAdministrationStatus.NotFound, null, []);
        }

        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return new(UserAdministrationStatus.NotFound, null, []);
        }

        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null) return new(UserAdministrationStatus.NotFound, null, []);
        var targetCompanyId = user.IsPlatformAdministrator
            ? null
            : tenant.IsPlatformAdministrator ? request.CompanyId : tenant.CompanyId;
        if (!user.IsPlatformAdministrator && (targetCompanyId is null ||
            !await context.Companies.AnyAsync(company => company.Id == targetCompanyId && company.IsActive, cancellationToken)))
        {
            return new(UserAdministrationStatus.CompanyNotFound, null, []);
        }
        var locationIds = request.LocationIds is null
            ? await context.UserLocations.Where(x => x.UserId == user.Id).Select(x => x.LocationId).ToArrayAsync(cancellationToken)
            : request.LocationIds.Distinct().ToArray();
        if (!user.IsPlatformAdministrator && (locationIds.Length == 0 || await context.CompanyLocations.CountAsync(
                location => locationIds.Contains(location.Id) && location.CompanyId == targetCompanyId && location.IsActive,
                cancellationToken) != locationIds.Length))
        {
            return new(UserAdministrationStatus.CompanyNotFound, null, []);
        }

        if (!IdentityEditorVersion.Matches(
                user.ConcurrencyStamp ?? string.Empty,
                request.EditorVersion))
        {
            return new(UserAdministrationStatus.Conflict, null, []);
        }

        var changedFields = ChangedFields(user, request);
        if (changedFields.Count == 0)
        {
            return new(
                UserAdministrationStatus.NoChanges,
                await userDirectory.GetByIdAsync(user.Id, cancellationToken),
                []);
        }

        var emailChanged = !string.Equals(
            user.Email,
            request.Email,
            StringComparison.OrdinalIgnoreCase);
        var passwordChanged = !string.IsNullOrWhiteSpace(request.Password);

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.Email = request.Email;
        user.UserName = request.Email;
        user.PhoneNumber = request.PhoneNumber;
        user.CompanyId = targetCompanyId;
        user.UpdatedAtUtc = timeProvider.GetUtcNow();

        if (emailChanged)
        {
            // An edited email is a new address and must be verified separately.
            // Only administrator-created accounts are trusted at initial provisioning.
            user.EmailConfirmed = false;
            if (user.MfaMethod == UserMfaMethod.Email)
            {
                user.TwoFactorEnabled = false;
            }
        }

        var profileResult = await userManager.UpdateAsync(user);
        if (!profileResult.Succeeded)
        {
            return new(MapUpdateFailure(profileResult), null, []);
        }

        if (!user.IsPlatformAdministrator)
        {
            var existingLocations = await context.UserLocations.Where(x => x.UserId == user.Id).ToArrayAsync(cancellationToken);
            context.UserLocations.RemoveRange(existingLocations.Where(x => !locationIds.Contains(x.LocationId)));
            var existingIds = existingLocations.Select(x => x.LocationId).ToHashSet();
            await context.UserLocations.AddRangeAsync(locationIds.Where(id => !existingIds.Contains(id)).Select(id => new ApplicationUserLocation { UserId = user.Id, LocationId = id }), cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        if (passwordChanged)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await userManager.ResetPasswordAsync(
                user,
                resetToken,
                request.Password!);

            if (!passwordResult.Succeeded)
            {
                return new(MapPasswordFailure(passwordResult), null, []);
            }
        }

        // ResetPasswordAsync already rotates the security stamp.
        // Email changes also revoke existing sessions, even when the password is unchanged.
        if (emailChanged && !passwordChanged)
        {
            var stampResult = await userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                return new(UserAdministrationStatus.Conflict, null, []);
            }
        }

        return new(
            UserAdministrationStatus.Succeeded,
            await userDirectory.GetByIdAsync(user.Id, cancellationToken),
            changedFields);
    }

    private static UserAdministrationStatus MapUpdateFailure(IdentityResult result)
    {
        if (result.Errors.Any(error =>
                string.Equals(
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

    private static UserAdministrationStatus MapPasswordFailure(IdentityResult result) =>
        result.Errors.Any(error =>
            error.Code.StartsWith("Password", StringComparison.Ordinal))
            ? UserAdministrationStatus.InvalidPassword
            : UserAdministrationStatus.Conflict;

    private static List<string> ChangedFields(
        ApplicationUser user,
        UserGeneralUpdateRequest request)
    {
        var fields = new List<string>(5);
        AddIfChanged(fields, "FirstName", user.FirstName, request.FirstName);
        AddIfChanged(fields, "LastName", user.LastName, request.LastName);
        AddIfChanged(fields, "Email", user.Email, request.Email);
        AddIfChanged(fields, "PhoneNumber", user.PhoneNumber, request.PhoneNumber);
        if (!user.IsPlatformAdministrator && user.CompanyId != request.CompanyId) fields.Add("CompanyId");

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            fields.Add("Password");
        }

        return fields;
    }

    private static void AddIfChanged(
        List<string> fields,
        string field,
        string? current,
        string? requested)
    {
        if (!string.Equals(
                current ?? string.Empty,
                requested ?? string.Empty,
                StringComparison.Ordinal))
        {
            fields.Add(field);
        }
    }
}
