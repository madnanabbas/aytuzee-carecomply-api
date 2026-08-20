using CareComply.Application.Abstractions.Identity;
using System.Diagnostics;
using Mediator;

namespace CareComply.Application.Features.Authentication.Commands.VerifyMfaRecoveryCode;

[DebuggerDisplay("VerifyMfaRecoveryCodeCommand { RecoveryCode=[REDACTED] }")]
public sealed class VerifyMfaRecoveryCodeCommand : ICommand<MfaSignInStatus>
{
    public VerifyMfaRecoveryCodeCommand(string recoveryCode) => RecoveryCode = recoveryCode;

    public string RecoveryCode { get; }

    public override string ToString() =>
        "VerifyMfaRecoveryCodeCommand { RecoveryCode = [REDACTED] }";
}
