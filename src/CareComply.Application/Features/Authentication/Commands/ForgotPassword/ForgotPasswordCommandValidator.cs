using FluentValidation;

namespace CareComply.Application.Features.Authentication.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator() => RuleFor(command => command.Email)
        .NotEmpty()
        .MaximumLength(256)
        .EmailAddress();
}
