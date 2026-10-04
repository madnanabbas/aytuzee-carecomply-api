using System.Diagnostics;
using System.Net;

namespace CareComply.Infrastructure.Clients;

internal static class ClientGuestInvitationEmailTemplate
{
    public static ClientGuestInvitationEmailContent Create(
        string clientName,
        string guestLink,
        DateTimeOffset expiresAtUtc)
    {
        var safeName = WebUtility.HtmlEncode(clientName);
        var safeLink = WebUtility.HtmlEncode(guestLink);
        var expiry = expiresAtUtc.UtcDateTime.ToString("MMMM d, yyyy 'at' HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);
        var safeExpiry = WebUtility.HtmlEncode(expiry);
        var plainText = $"""
            CareComply Client Intake Invitation

            An intake form has been prepared for {clientName}.

            Open the secure form:
            {guestLink}

            This link expires on {expiry}. Do not share this link with anyone.
            If you were not expecting this invitation, please ignore this email and contact the organization that sent it.
            """;
        var html = $$"""
            <!doctype html>
            <html lang="en">
            <body style="margin:0;padding:24px;background:#f8fafc;color:#0f172a;font-family:Arial,sans-serif">
              <table role="presentation" width="100%"><tr><td align="center">
                <table role="presentation" width="620" style="max-width:620px;background:#fff;border:1px solid #cbd5e1;border-radius:8px">
                  <tr><td style="padding:32px">
                    <h1 style="margin:0 0 16px;color:#0f766e">CareComply Client Intake Invitation</h1>
                    <p>An intake form has been prepared for <strong>{{safeName}}</strong>.</p>
                    <p style="margin:24px 0"><a href="{{safeLink}}" style="display:inline-block;padding:12px 20px;background:#0f766e;color:#fff;text-decoration:none;border-radius:6px">Open secure intake form</a></p>
                    <p>This secure link expires on <strong>{{safeExpiry}}</strong>.</p>
                    <p>Do not share this link. If you were not expecting this invitation, ignore this email and contact the organization that sent it.</p>
                  </td></tr>
                </table>
              </td></tr></table>
            </body>
            </html>
            """;
        return new("Complete your CareComply client intake", plainText, html);
    }
}

[DebuggerDisplay("ClientGuestInvitationEmailContent { Subject={Subject}, PlainText=[REDACTED], Html=[REDACTED] }")]
internal sealed record ClientGuestInvitationEmailContent(string Subject, string PlainText, string Html)
{
    public override string ToString() =>
        $"ClientGuestInvitationEmailContent {{ Subject = {Subject}, PlainText = [REDACTED], Html = [REDACTED] }}";
}
