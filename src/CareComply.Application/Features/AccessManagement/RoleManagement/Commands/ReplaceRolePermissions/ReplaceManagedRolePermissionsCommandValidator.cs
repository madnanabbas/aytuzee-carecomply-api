using FluentValidation;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Commands.ReplaceRolePermissions;

public sealed class ReplaceManagedRolePermissionsCommandValidator : AbstractValidator<ReplaceManagedRolePermissionsCommand>
{
    public ReplaceManagedRolePermissionsCommandValidator()
    {
        RuleFor(command => command.RoleId).NotEmpty();
        RuleFor(command => command.PermissionIds).NotNull();
        RuleForEach(command => command.PermissionIds).GreaterThan(0);
        RuleFor(command => command.PermissionIds).Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Permission identifiers must be unique.");
        RuleFor(command => command.EditorVersion).NotEmpty().Length(64);
    }
}
