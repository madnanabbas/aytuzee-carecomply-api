using CareComply.Application.Abstractions.Identity;
using FluentValidation;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.SetUserMfa;

public sealed class SetUserMfaCommandValidator : AbstractValidator<SetUserMfaCommand>
{
    public SetUserMfaCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Method).IsInEnum();
        RuleFor(command => command.EditorVersion).NotEmpty().Length(64);
    }
}
