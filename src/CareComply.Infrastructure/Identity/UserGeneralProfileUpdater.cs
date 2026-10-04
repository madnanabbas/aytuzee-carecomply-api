using CareComply.Application.Abstractions.Identity;

namespace CareComply.Infrastructure.Identity;

internal static class UserGeneralProfileUpdater
{
    public static IReadOnlyList<string> GetChangedFields(
        ApplicationUser user,
        UserGeneralUpdateRequest request)
    {
        var fields = new List<string>(5);
        AddIfChanged(fields, "FirstName", user.FirstName, request.FirstName);
        AddIfChanged(fields, "LastName", user.LastName, request.LastName);
        AddIfChanged(fields, "Email", user.Email, request.Email);
        AddIfChanged(fields, "PhoneNumber", user.PhoneNumber, request.PhoneNumber);
        if (!user.IsPlatformAdministrator && user.CompanyId != request.CompanyId)
        {
            fields.Add("CompanyId");
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            fields.Add("Password");
        }

        return fields;
    }

    public static bool Apply(
        ApplicationUser user,
        UserGeneralUpdateRequest request,
        Guid? companyId,
        DateTimeOffset updatedAtUtc)
    {
        var emailChanged = !string.Equals(
            user.Email,
            request.Email,
            StringComparison.OrdinalIgnoreCase);

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.Email = request.Email;
        user.UserName = request.Email;
        user.PhoneNumber = request.PhoneNumber;
        user.CompanyId = companyId;
        user.UpdatedAtUtc = updatedAtUtc;

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

        return emailChanged;
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
