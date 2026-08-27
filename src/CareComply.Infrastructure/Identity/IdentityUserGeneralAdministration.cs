using CareComply.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Identity;

namespace CareComply.Infrastructure.Identity;

internal sealed class IdentityUserGeneralAdministration(
    UserManager<ApplicationUser> userManager,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IUserGeneralAdministration
{
    public async Task<UserGeneralUpdateResult> UpdateAsync(
        UserGeneralUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return new(UserAdministrationStatus.NotFound, null, []);
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
