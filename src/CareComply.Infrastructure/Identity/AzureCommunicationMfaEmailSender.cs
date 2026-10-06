using System.Diagnostics;
using System.Net;
using Azure.Communication.Email;
using CareComply.Application.Abstractions.Identity;

namespace CareComply.Infrastructure.Identity;

internal sealed class AzureCommunicationMfaEmailSender(
    AzureCommunicationEmailDelivery emailDelivery) : IMfaEmailSender
{
    public Task<bool> SendCodeAsync(
        string recipientAddress,
        string code,
        CancellationToken cancellationToken)
    {
        var template = MfaEmailTemplate.Create(code);
        var content = new EmailContent(template.Subject)
        {
            PlainText = template.PlainText,
            Html = template.Html,
        };
        return emailDelivery.SendAsync(
            recipientAddress,
            content,
            [PasswordRecoveryEmailBranding.CreateLogoAttachment()],
            cancellationToken);
    }
}

[DebuggerDisplay("MfaEmailTemplate { Content=[REDACTED] }")]
internal sealed class MfaEmailTemplate
{
    private MfaEmailTemplate(string subject, string plainText, string html)
    {
        Subject = subject;
        PlainText = plainText;
        Html = html;
    }

    public string Subject { get; }

    public string PlainText { get; }

    public string Html { get; }

    public static MfaEmailTemplate Create(string code)
    {
        var encodedCode = WebUtility.HtmlEncode(code);
        return new(
            "Your CareComply verification code",
            $"Your CareComply verification code is: {code}\n\nEnter this code to finish signing in. If you did not attempt to sign in, do not share this code and contact your administrator.",
            $$"""
            <!doctype html>
            <html lang="en">
              <body style="margin:0;background:#F8FAFC;font-family:Arial,sans-serif;color:#0F172A">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="padding:32px 16px;background:#F8FAFC">
                  <tr><td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;background:#FFFFFF;border:1px solid #CBD5E1;border-radius:8px">
                      <tr><td style="padding:28px 32px 16px">
                        <img src="cid:aytuzee-carecomply-logo" alt="Aytuzee Care Comply" width="220" style="display:block;max-width:100%;height:auto">
                      </td></tr>
                      <tr><td style="padding:16px 32px 32px">
                        <h1 style="margin:0 0 12px;font-size:24px;line-height:32px">Verify your sign-in</h1>
                        <p style="margin:0 0 24px;font-size:15px;line-height:24px;color:#475569">Enter this one-time code to finish signing in to CareComply.</p>
                        <p style="margin:0;padding:16px;text-align:center;background:#F0FDFA;border:1px solid #99F6E4;border-radius:6px;font-family:Consolas,monospace;font-size:28px;font-weight:600;letter-spacing:8px;color:#0F766E">{{encodedCode}}</p>
                        <p style="margin:24px 0 0;font-size:13px;line-height:20px;color:#64748B">If you did not attempt to sign in, do not share this code and contact your administrator.</p>
                      </td></tr>
                    </table>
                  </td></tr>
                </table>
              </body>
            </html>
            """);
    }

    public override string ToString() => "MfaEmailTemplate { Content = [REDACTED] }";
}
