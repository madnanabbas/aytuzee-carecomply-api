using FluentValidation;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.ReplaceUserRoles;

public sealed class ReplaceUserRolesCommandValidator : AbstractValidator<ReplaceUserRolesCommand>
{
    private const int MaximumRoleCount = 10;

    public ReplaceUserRolesCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.EditorVersion).NotEmpty().Length(64);
        RuleFor(command => command.RoleIds)
            .NotEmpty()
            .Must(roleIds => roleIds.Count <= MaximumRoleCount)
            .WithMessage($"No more than {MaximumRoleCount} roles may be assigned.")
            .Must(roleIds => roleIds.Distinct().Count() == roleIds.Count)
            .WithMessage("Duplicate role identifiers are not allowed.");
        RuleForEach(command => command.RoleIds).NotEmpty();
    }
}
