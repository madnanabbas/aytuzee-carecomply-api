using CareComply.Application.Abstractions.Identity;
using FluentValidation;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.CreateUser;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    private const int MaximumRoleCount = 10;

    public CreateUserCommandValidator()
    {
        RuleFor(command => command.FirstName)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("First name is required.")
            .MaximumLength(100);
        RuleFor(command => command.LastName)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Last name is required.")
            .MaximumLength(100);
        RuleFor(command => command.Email).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(UserPasswordPolicy.MinimumLength)
            .MaximumLength(UserPasswordPolicy.MaximumLength);
        RuleFor(command => command.MfaMethod).IsInEnum();
        RuleFor(command => command.PhoneNumber)
            .MaximumLength(32)
            .Matches(@"^\+?[0-9 ()-]{7,32}$")
            .When(command => !string.IsNullOrWhiteSpace(command.PhoneNumber));
        RuleFor(command => command.RoleIds)
            .NotEmpty()
            .Must(roleIds => roleIds.Count <= MaximumRoleCount)
            .WithMessage($"No more than {MaximumRoleCount} roles may be assigned.")
            .Must(roleIds => roleIds.Distinct().Count() == roleIds.Count)
            .WithMessage("Duplicate role identifiers are not allowed.");
        RuleForEach(command => command.RoleIds).NotEmpty();
    }
}
