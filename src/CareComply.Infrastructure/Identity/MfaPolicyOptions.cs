namespace CareComply.Infrastructure.Identity;

public sealed class MfaPolicyOptions
{
    public const string SectionName = "Authentication:Mfa";

    public string Issuer { get; set; } = string.Empty;

    public string[] RequiredPermissions { get; set; } = [];
}
