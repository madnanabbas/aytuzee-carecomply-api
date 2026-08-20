using FluentValidation;

namespace CareComply.Application.Features.Authentication.Commands.VerifyMfaRecoveryCode;

public sealed class VerifyMfaRecoveryCodeCommandValidator
    : AbstractValidator<VerifyMfaRecoveryCodeCommand>
{
    public VerifyMfaRecoveryCodeCommandValidator() =>
        RuleFor(command => command.RecoveryCode).NotEmpty().MaximumLength(64);
}
