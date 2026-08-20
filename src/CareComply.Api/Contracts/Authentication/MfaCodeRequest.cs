using System.Diagnostics;

namespace CareComply.Api.Contracts.Authentication;

[DebuggerDisplay("MfaCodeRequest { Code=[REDACTED] }")]
public sealed class MfaCodeRequest
{
    public string? Code { get; init; }

    public override string ToString() => "MfaCodeRequest { Code = [REDACTED] }";
}
