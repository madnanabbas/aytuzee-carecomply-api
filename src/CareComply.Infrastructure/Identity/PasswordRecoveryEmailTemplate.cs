using System.Diagnostics;
using System.Net;

namespace CareComply.Infrastructure.Identity;

public static class PasswordRecoveryEmailTemplate
{
    public static PasswordRecoveryEmailContent Create(string resetUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resetUrl);
        var encodedUrl = WebUtility.HtmlEncode(resetUrl);

        var plainText = $"""
            CareComply
            Secure & Confidential

            Reset your password

            Hello,

            We received a request to reset the password for your CareComply account.
            Use the secure link below to create a new password:

            {resetUrl}

            This link expires in 60 minutes for your security.
            If you did not request a password reset, you can safely ignore this email.
            Do not share this link with anyone.

            This is an automated security email from CareComply. Please do not reply.
            """;

        var html = $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Reset your CareComply password</title>
              <style>
                @media only screen and (max-width: 620px) {
                  .email-shell { width: 100% !important; }
                  .email-pad { padding-left: 20px !important; padding-right: 20px !important; }
                  .header-cell { display: block !important; width: 100% !important; text-align: left !important; }
                  .security-cell { padding-top: 18px !important; }
                  .trust-cell { display: block !important; width: 100% !important; border-left: 0 !important; border-top: 1px solid #CBD5E1 !important; }
                  .trust-cell:first-child { border-top: 0 !important; }
                  .action-button { width: 100% !important; box-sizing: border-box !important; }
                }
              </style>
            </head>
            <body style="margin:0;padding:0;background:#F8FAFC;color:#0F172A;font-family:Arial,'Helvetica Neue',sans-serif;">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">
                A secure CareComply password-reset link valid for 60 minutes.
              </div>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="width:100%;background:#F8FAFC;">
                <tr><td height="8" style="height:8px;background:#0F766E;font-size:0;line-height:0;">&nbsp;</td></tr>
                <tr>
                  <td align="center" class="email-pad" style="padding:32px 20px 40px;">
                    <table role="presentation" class="email-shell" width="680" cellpadding="0" cellspacing="0" border="0" style="width:680px;max-width:680px;">
                      <tr>
                        <td class="header-cell" width="58%" valign="middle" style="padding:0 0 26px;">
                          <table role="presentation" cellpadding="0" cellspacing="0" border="0">
                            <tr>
                              <td valign="middle">
                                <img src="cid:aytuzee-carecomply-logo" width="250" alt="Aytuzee Care Comply" style="display:block;width:250px;max-width:100%;height:auto;border:0;outline:none;text-decoration:none;">
                                <div style="margin-top:3px;font-size:13px;line-height:18px;color:#526176;">Compliance. Simplified. Care. Elevated.</div>
                              </td>
                            </tr>
                          </table>
                        </td>
                        <td class="header-cell security-cell" width="42%" valign="middle" align="right" style="padding:0 0 26px;">
                          <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="display:inline-table;">
                            <tr>
                              <td valign="middle" style="padding-right:10px;color:#0F766E;font-size:27px;line-height:27px;">&#128737;</td>
                              <td valign="middle" align="left">
                                <div style="font-size:14px;line-height:20px;font-weight:700;color:#0F172A;">Secure &amp; Confidential</div>
                                <div style="font-size:12px;line-height:18px;color:#526176;">Your account security is our priority.</div>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td colspan="2" style="background:#FFFFFF;border:1px solid #CBD5E1;border-radius:8px;overflow:hidden;">
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                            <tr>
                              <td align="center" class="email-pad" style="padding:42px 48px 34px;">
                                <div style="display:inline-block;width:76px;height:76px;line-height:76px;border-radius:50%;background:#F0FDFA;color:#0F766E;font-size:38px;text-align:center;">&#128274;</div>
                                <h1 style="margin:20px 0 0;font-size:30px;line-height:38px;font-weight:700;letter-spacing:-0.4px;color:#0F172A;">Reset Your Password</h1>
                                <div style="width:42px;height:3px;margin:16px auto 18px;background:#0F766E;font-size:0;line-height:0;">&nbsp;</div>
                                <p style="margin:0;font-size:17px;line-height:25px;font-weight:700;color:#0F172A;">Hello,</p>
                                <p style="max-width:520px;margin:8px auto 0;font-size:15px;line-height:24px;color:#334155;">
                                  We received a request to reset the password for your CareComply account.<br>
                                  Select the button below to create a new password.
                                </p>
                                <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:26px auto 0;">
                                  <tr>
                                    <td align="center" bgcolor="#0F766E" style="border-radius:6px;">
                                      <a class="action-button" href="{{encodedUrl}}" target="_blank" style="display:inline-block;min-width:300px;padding:15px 26px;border:1px solid #0F766E;border-radius:6px;background:#0F766E;color:#FFFFFF;font-size:16px;line-height:20px;font-weight:700;text-align:center;text-decoration:none;">&#128274;&nbsp;&nbsp; Reset My Password</a>
                                    </td>
                                  </tr>
                                </table>
                                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-top:28px;background:#F0FDFA;border-radius:8px;">
                                  <tr>
                                    <td width="52" valign="middle" align="center" style="padding:16px 0 16px 14px;color:#0F766E;font-size:27px;">&#9201;</td>
                                    <td valign="middle" align="left" style="padding:16px 18px 16px 8px;font-size:13px;line-height:20px;color:#334155;">
                                      This link expires in <strong style="color:#0F766E;">60 minutes</strong> for your security.<br>
                                      If you did not request a password reset, you can safely ignore this email.
                                    </td>
                                  </tr>
                                </table>
                              </td>
                            </tr>
                            <tr>
                              <td style="padding:0 24px 30px;">
                                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#F8FAFC;border-radius:8px;">
                                  <tr>
                                    <td class="trust-cell" width="33.33%" valign="top" style="padding:20px;">
                                      <div style="font-size:22px;line-height:24px;color:#0F766E;">&#128737;</div>
                                      <div style="margin-top:8px;font-size:14px;line-height:20px;font-weight:700;color:#0F766E;">Secure</div>
                                      <div style="margin-top:3px;font-size:12px;line-height:18px;color:#334155;">Protected account recovery using CareComply security controls.</div>
                                    </td>
                                    <td class="trust-cell" width="33.33%" valign="top" style="padding:20px;border-left:1px solid #CBD5E1;">
                                      <div style="font-size:22px;line-height:24px;color:#0F766E;">&#128100;</div>
                                      <div style="margin-top:8px;font-size:14px;line-height:20px;font-weight:700;color:#0F766E;">Private</div>
                                      <div style="margin-top:3px;font-size:12px;line-height:18px;color:#334155;">This link is unique to you and should not be shared.</div>
                                    </td>
                                    <td class="trust-cell" width="33.33%" valign="top" style="padding:20px;border-left:1px solid #CBD5E1;">
                                      <div style="font-size:22px;line-height:24px;color:#0F766E;">&#9201;</div>
                                      <div style="margin-top:8px;font-size:14px;line-height:20px;font-weight:700;color:#0F766E;">Time-limited</div>
                                      <div style="margin-top:3px;font-size:12px;line-height:18px;color:#334155;">The secure reset link expires after 60 minutes.</div>
                                    </td>
                                  </tr>
                                </table>
                              </td>
                            </tr>
                            <tr>
                              <td style="padding:22px 28px;border-top:1px solid #CBD5E1;background:#F8FAFC;">
                                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                                  <tr>
                                    <td valign="middle">
                                      <img src="cid:aytuzee-carecomply-logo" width="150" alt="Aytuzee Care Comply" style="display:block;width:150px;max-width:100%;height:auto;border:0;outline:none;text-decoration:none;">
                                      <div style="margin-top:4px;font-size:12px;line-height:18px;color:#526176;">Secure compliance and account management.</div>
                                    </td>
                                    <td valign="middle" align="right" style="font-size:12px;line-height:18px;color:#526176;">Automated security notification</td>
                                  </tr>
                                </table>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td colspan="2" align="center" style="padding:18px 16px 0;font-size:11px;line-height:17px;color:#526176;">
                          This email was sent from an automated system. Please do not reply.<br>
                          CareComply account security
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;

        return new PasswordRecoveryEmailContent(
            "Reset your CareComply password",
            plainText,
            html);
    }
}

[DebuggerDisplay("PasswordRecoveryEmailContent { Subject={Subject}, PlainText=[REDACTED], Html=[REDACTED] }")]
public sealed class PasswordRecoveryEmailContent(
    string subject,
    string plainText,
    string html)
{
    public string Subject { get; } = subject;

    public string PlainText { get; } = plainText;

    public string Html { get; } = html;

    public override string ToString() =>
        $"PasswordRecoveryEmailContent {{ Subject = {Subject}, PlainText = [REDACTED], Html = [REDACTED] }}";
}
