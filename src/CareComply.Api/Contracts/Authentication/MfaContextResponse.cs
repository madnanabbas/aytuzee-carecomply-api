using System.Diagnostics;

namespace CareComply.Api.Contracts.Authentication;

[DebuggerDisplay("MfaContextResponse { Status={Status}, Method={Method}, RequestToken=[REDACTED] }")]
public sealed class MfaContextResponse
{
    public MfaContextResponse(string status, string method, string? requestToken)
    {
        Status = status;
        Method = method;
        RequestToken = requestToken;
    }

    public string Status { get; }

    public string Method { get; }

    public string? RequestToken { get; }

    public override string ToString() =>
        $"MfaContextResponse {{ Status = {Status}, Method = {Method}, RequestToken = [REDACTED] }}";
}
