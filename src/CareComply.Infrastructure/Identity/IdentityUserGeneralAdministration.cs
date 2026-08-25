using CareComply.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Identity;

namespace CareComply.Infrastructure.Identity;

internal sealed class IdentityUserGeneralAdministration(
    UserManager<ApplicationUser> userManager,
    IUserDirectory userDirectory) : IUserGeneralAdministration
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

        if (!IdentityEditorVersion.Matches(user.ConcurrencyStamp ?? string.Empty, request.EditorVersion))
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

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.Email = request.Email;
        user.UserName = request.Email;
        user.PhoneNumber = request.PhoneNumber;
        if (emailChanged)
        {
            user.EmailConfirmed = false;
            if (user.MfaMethod == UserMfaMethod.Email)
            {
                user.TwoFactorEnabled = false;
            }
        }
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var status = result.Errors.Any(error =>
                string.Equals(error.Code, nameof(IdentityErrorDescriber.ConcurrencyFailure), StringComparison.Ordinal))
                ? UserAdministrationStatus.Conflict
                : result.Errors.Any(error => error.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase))
                    ? UserAdministrationStatus.DuplicateEmail
                    : UserAdministrationStatus.Conflict;
            return new(status, null, []);
        }

        if (emailChanged)
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

    private static List<string> ChangedFields(
        ApplicationUser user,
        UserGeneralUpdateRequest request)
    {
        var fields = new List<string>(5);
        AddIfChanged(fields, "FirstName", user.FirstName, request.FirstName);
        AddIfChanged(fields, "LastName", user.LastName, request.LastName);
        AddIfChanged(fields, "Email", user.Email, request.Email);
        AddIfChanged(fields, "PhoneNumber", user.PhoneNumber, request.PhoneNumber);
        return fields;
    }

    private static void AddIfChanged(
        List<string> fields,
        string field,
        string? current,
        string? requested)
    {
        if (!string.Equals(current ?? string.Empty, requested ?? string.Empty, StringComparison.Ordinal))
        {
            fields.Add(field);
        }
    }
}
