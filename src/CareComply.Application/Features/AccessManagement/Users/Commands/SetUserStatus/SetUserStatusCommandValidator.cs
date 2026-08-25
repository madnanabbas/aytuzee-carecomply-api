using FluentValidation;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.SetUserStatus;

public sealed class SetUserStatusCommandValidator : AbstractValidator<SetUserStatusCommand>
{
    public SetUserStatusCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.EditorVersion).NotEmpty().Length(64);
    }
}
