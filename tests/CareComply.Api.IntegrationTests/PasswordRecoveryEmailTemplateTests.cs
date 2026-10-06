using CareComply.Infrastructure.Identity;

namespace CareComply.Api.IntegrationTests;

public sealed class PasswordRecoveryEmailTemplateTests
{
    private const string ResetUrl =
        "https://carecomply.example.test/auth/reset-password?userId=opaque-id&token=opaque-token";

    [Fact]
    public void TemplateContainsApprovedSecurityContentAndAction()
    {
        var content = PasswordRecoveryEmailTemplate.Create(ResetUrl);

        Assert.Equal("Reset your CareComply password", content.Subject);
        Assert.Contains("CareComply", content.Html, StringComparison.Ordinal);
        Assert.Contains("Secure &amp; Confidential", content.Html, StringComparison.Ordinal);
        Assert.Contains("Reset My Password", content.Html, StringComparison.Ordinal);
        Assert.Contains("60 minutes", content.Html, StringComparison.Ordinal);
        Assert.Contains("Do not share", content.PlainText, StringComparison.Ordinal);
        Assert.Contains("token=opaque-token", content.PlainText, StringComparison.Ordinal);
        Assert.Contains("token=opaque-token", content.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplateIsSelfContainedAndDoesNotInventContactDetails()
    {
        var html = PasswordRecoveryEmailTemplate.Create(ResetUrl).Html;

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "src=\"cid:aytuzee-carecomply-logo\"",
            html,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("src=\"http", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gradient", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("support@", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("+1 (", html, StringComparison.Ordinal);
        Assert.DoesNotContain("linkedin", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Austin", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApprovedAytuzeeLogoIsEmbeddedForInlineDelivery()
    {
        const string resourceName =
            "CareComply.Infrastructure.Identity.EmailAssets.aytuzee-carecomply-logo.png";
        using var stream = typeof(PasswordRecoveryEmailTemplate).Assembly
            .GetManifestResourceStream(resourceName);

        Assert.NotNull(stream);
        Span<byte> signature = stackalloc byte[8];
        Assert.Equal(signature.Length, stream.Read(signature));
        Assert.Equal(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
            signature.ToArray());
    }

    [Fact]
    public void TemplateEncodesResetUrlAndRedactsDebugFormatting()
    {
        const string url = "https://carecomply.example.test/reset?token=value&next=\"unsafe\"";

        var content = PasswordRecoveryEmailTemplate.Create(url);

        Assert.Contains("token=value&amp;next=&quot;unsafe&quot;", content.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("value", content.ToString(), StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", content.ToString(), StringComparison.Ordinal);
    }
}
