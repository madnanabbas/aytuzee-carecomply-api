using FluentValidation;

namespace CareComply.Application.Features.Authentication.Commands.ConfirmMfaEnrollment;

public sealed class ConfirmMfaEnrollmentCommandValidator
    : AbstractValidator<ConfirmMfaEnrollmentCommand>
{
    public ConfirmMfaEnrollmentCommandValidator() =>
        RuleFor(command => command.Code).NotEmpty().MaximumLength(32);
}
