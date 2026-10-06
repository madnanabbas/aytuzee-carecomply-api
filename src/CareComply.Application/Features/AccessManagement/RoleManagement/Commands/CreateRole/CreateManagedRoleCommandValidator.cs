using CareComply.Domain.AccessManagement;
using FluentValidation;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Commands.CreateRole;

public sealed class CreateManagedRoleCommandValidator : AbstractValidator<CreateManagedRoleCommand>
{
    public CreateManagedRoleCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(RoleDefinition.MaximumNameLength);
        RuleFor(command => command.Description).MaximumLength(RoleDefinition.MaximumDescriptionLength);
        RuleFor(command => command.PermissionIds).NotNull();
        RuleForEach(command => command.PermissionIds).GreaterThan(0);
        RuleFor(command => command.PermissionIds).Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Permission identifiers must be unique.");
    }
}
