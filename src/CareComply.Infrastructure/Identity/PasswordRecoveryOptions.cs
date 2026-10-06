namespace CareComply.Infrastructure.Identity;

public sealed class PasswordRecoveryOptions
{
    public const string SectionName = "PasswordRecovery";

    public string FrontendBaseUrl { get; init; } = string.Empty;
}
