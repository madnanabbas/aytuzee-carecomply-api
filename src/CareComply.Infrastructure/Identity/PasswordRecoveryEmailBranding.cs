using System.Reflection;
using Azure.Communication.Email;

namespace CareComply.Infrastructure.Identity;

internal static class PasswordRecoveryEmailBranding
{
    public const string LogoContentId = "aytuzee-carecomply-logo";

    private const string LogoResourceName =
        "CareComply.Infrastructure.Identity.EmailAssets.aytuzee-carecomply-logo.png";

    public static EmailAttachment CreateLogoAttachment()
    {
        using var stream = typeof(PasswordRecoveryEmailBranding).Assembly
            .GetManifestResourceStream(LogoResourceName)
            ?? throw new InvalidOperationException(
                "The embedded password-recovery email logo is unavailable.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return new EmailAttachment(
            "aytuzee-carecomply-logo.png",
            "image/png",
            BinaryData.FromBytes(buffer.ToArray()))
        {
            ContentId = LogoContentId,
        };
    }
}
