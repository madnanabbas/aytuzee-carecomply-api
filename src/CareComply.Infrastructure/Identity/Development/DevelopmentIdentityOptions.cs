namespace CareComply.Infrastructure.Identity.Development;

public sealed class DevelopmentIdentityOptions
{
    public const string SectionName = "DevelopmentFixtures:Identity";

    public bool Enabled { get; set; }

    public DevelopmentIdentityUserPasswords Users { get; set; } = new();
}

public sealed class DevelopmentIdentityUserPasswords
{
    public string? SystemAdministratorPassword { get; set; }

    public string? ComplianceManagerPassword { get; set; }

    public string? HrManagerPassword { get; set; }

    public string? ReadOnlyUserPassword { get; set; }
}
