using System.Diagnostics;

namespace CareComply.Api.Contracts.Authentication;

[DebuggerDisplay("MfaEnrollmentConfirmedResponse { RecoveryCodes=[REDACTED] }")]
public sealed class MfaEnrollmentConfirmedResponse
{
    public MfaEnrollmentConfirmedResponse(IReadOnlyList<string> recoveryCodes) =>
        RecoveryCodes = recoveryCodes;

    public IReadOnlyList<string> RecoveryCodes { get; }

    public override string ToString() =>
        "MfaEnrollmentConfirmedResponse { RecoveryCodes = [REDACTED] }";
}
