namespace CareComply.Infrastructure.Identity.Staging;

public sealed class StagingIdentityOptions
{
    public const string SectionName = "StagingIdentity";

    public bool Enabled { get; init; }

    public StagingIdentityUserPasswords Users { get; init; } = new();
}

public sealed class StagingIdentityUserPasswords
{
    public string? SystemAdministratorPassword { get; init; }

    public string? ComplianceManagerPassword { get; init; }

    public string? HrManagerPassword { get; init; }

    public string? ReadOnlyUserPassword { get; init; }
}