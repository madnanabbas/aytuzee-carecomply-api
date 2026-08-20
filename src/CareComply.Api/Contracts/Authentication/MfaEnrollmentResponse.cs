using System.Diagnostics;

namespace CareComply.Api.Contracts.Authentication;

[DebuggerDisplay("MfaEnrollmentResponse { ManualSetupKey=[REDACTED], QrCodeDataUrl=[REDACTED] }")]
public sealed class MfaEnrollmentResponse
{
    public MfaEnrollmentResponse(string manualSetupKey, string qrCodeDataUrl)
    {
        ManualSetupKey = manualSetupKey;
        QrCodeDataUrl = qrCodeDataUrl;
    }

    public string ManualSetupKey { get; }

    public string QrCodeDataUrl { get; }

    public override string ToString() =>
        "MfaEnrollmentResponse { ManualSetupKey = [REDACTED], QrCodeDataUrl = [REDACTED] }";
}
