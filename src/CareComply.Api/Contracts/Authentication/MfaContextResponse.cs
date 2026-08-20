using System.Diagnostics;

namespace CareComply.Api.Contracts.Authentication;

[DebuggerDisplay("MfaContextResponse { Status={Status}, RequestToken=[REDACTED] }")]
public sealed class MfaContextResponse
{
    public MfaContextResponse(string status, string? requestToken)
    {
        Status = status;
        RequestToken = requestToken;
    }

    public string Status { get; }

    public string? RequestToken { get; }

    public override string ToString() =>
        $"MfaContextResponse {{ Status = {Status}, RequestToken = [REDACTED] }}";
}
