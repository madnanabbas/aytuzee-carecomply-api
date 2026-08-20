using CareComply.Application.Abstractions.Identity;
using System.Diagnostics;
using Mediator;

namespace CareComply.Application.Features.Authentication.Commands.VerifyMfa;

[DebuggerDisplay("VerifyMfaCommand { Code=[REDACTED] }")]
public sealed class VerifyMfaCommand : ICommand<MfaSignInStatus>
{
    public VerifyMfaCommand(string code) => Code = code;

    public string Code { get; }

    public override string ToString() => "VerifyMfaCommand { Code = [REDACTED] }";
}
