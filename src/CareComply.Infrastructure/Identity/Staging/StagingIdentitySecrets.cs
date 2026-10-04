namespace CareComply.Infrastructure.Identity.Staging;

internal static class StagingIdentitySecrets
{
    public static IReadOnlyDictionary<StagingPasswordKey, string> ReadRequiredPasswords(
        StagingIdentityUserPasswords passwords) =>
        new Dictionary<StagingPasswordKey, string>
        {
            [StagingPasswordKey.SystemAdministrator] = RequirePassword(
                passwords.SystemAdministratorPassword,
                "Users:SystemAdministratorPassword"),
            [StagingPasswordKey.ComplianceManager] = RequirePassword(
                passwords.ComplianceManagerPassword,
                "Users:ComplianceManagerPassword"),
            [StagingPasswordKey.HrManager] = RequirePassword(
                passwords.HrManagerPassword,
                "Users:HrManagerPassword"),
            [StagingPasswordKey.ReadOnlyUser] = RequirePassword(
                passwords.ReadOnlyUserPassword,
                "Users:ReadOnlyUserPassword"),
        };

    private static string RequirePassword(string? password, string relativeKey)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                $"The required staging identity secret '{StagingIdentityOptions.SectionName}:{relativeKey}' is not configured.");
        }

        return password;
    }
}
