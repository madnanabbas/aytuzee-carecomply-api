using FluentValidation;

namespace CareComply.Application.Features.Authentication.Commands.ResetPassword;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Token).NotEmpty().MaximumLength(4096);
        RuleFor(command => command.NewPassword).NotEmpty().MinimumLength(15).MaximumLength(128);
        RuleFor(command => command.ConfirmPassword)
            .NotEmpty()
            .Equal(command => command.NewPassword)
            .WithMessage("Password confirmation must match the new password.");
    }
}
