using System.Diagnostics;

namespace CareComply.Api.Contracts.Authentication;

[DebuggerDisplay("MfaRecoveryCodeRequest { RecoveryCode=[REDACTED] }")]
public sealed class MfaRecoveryCodeRequest
{
    public string? RecoveryCode { get; init; }

    public override string ToString() =>
        "MfaRecoveryCodeRequest { RecoveryCode = [REDACTED] }";
}
