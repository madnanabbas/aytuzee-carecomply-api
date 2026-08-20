using CareComply.Application.Abstractions.Identity;
using System.Diagnostics;
using Mediator;

namespace CareComply.Application.Features.Authentication.Commands.ConfirmMfaEnrollment;

[DebuggerDisplay("ConfirmMfaEnrollmentCommand { Code=[REDACTED] }")]
public sealed class ConfirmMfaEnrollmentCommand : ICommand<MfaEnrollmentConfirmation>
{
    public ConfirmMfaEnrollmentCommand(string code) => Code = code;

    public string Code { get; }

    public override string ToString() =>
        "ConfirmMfaEnrollmentCommand { Code = [REDACTED] }";
}
