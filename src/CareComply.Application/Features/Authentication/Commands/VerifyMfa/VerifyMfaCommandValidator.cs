using FluentValidation;

namespace CareComply.Application.Features.Authentication.Commands.VerifyMfa;

public sealed class VerifyMfaCommandValidator : AbstractValidator<VerifyMfaCommand>
{
    public VerifyMfaCommandValidator() =>
        RuleFor(command => command.Code).NotEmpty().MaximumLength(32);
}
