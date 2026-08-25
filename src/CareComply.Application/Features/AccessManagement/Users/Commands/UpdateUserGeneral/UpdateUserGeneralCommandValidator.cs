using FluentValidation;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.UpdateUserGeneral;

public sealed class UpdateUserGeneralCommandValidator : AbstractValidator<UpdateUserGeneralCommand>
{
    public UpdateUserGeneralCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Email).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(command => command.PhoneNumber)
            .MaximumLength(32)
            .Matches(@"^\+?[0-9 ()-]{7,32}$")
            .When(command => !string.IsNullOrWhiteSpace(command.PhoneNumber));
        RuleFor(command => command.EditorVersion).NotEmpty().Length(64);
    }
}
