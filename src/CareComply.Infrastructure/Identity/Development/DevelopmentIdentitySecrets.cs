namespace CareComply.Infrastructure.Identity.Development;

internal static class DevelopmentIdentitySecrets
{
    public static IReadOnlyDictionary<DevelopmentPasswordKey, string> ReadRequiredPasswords(
        DevelopmentIdentityUserPasswords passwords) =>
        new Dictionary<DevelopmentPasswordKey, string>
        {
            [DevelopmentPasswordKey.SystemAdministrator] = RequirePassword(
                passwords.SystemAdministratorPassword,
                "Users:SystemAdministratorPassword"),
            [DevelopmentPasswordKey.ComplianceManager] = RequirePassword(
                passwords.ComplianceManagerPassword,
                "Users:ComplianceManagerPassword"),
            [DevelopmentPasswordKey.HrManager] = RequirePassword(
                passwords.HrManagerPassword,
                "Users:HrManagerPassword"),
            [DevelopmentPasswordKey.ReadOnlyUser] = RequirePassword(
                passwords.ReadOnlyUserPassword,
                "Users:ReadOnlyUserPassword"),
        };

    private static string RequirePassword(string? password, string relativeKey)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                $"The required development identity secret '{DevelopmentIdentityOptions.SectionName}:{relativeKey}' is not configured.");
        }

        return password;
    }
}
