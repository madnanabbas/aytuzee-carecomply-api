using CareComply.Domain.AccessManagement;
using FluentValidation;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Commands.UpdateRoleGeneral;

public sealed class UpdateManagedRoleGeneralCommandValidator : AbstractValidator<UpdateManagedRoleGeneralCommand>
{
    public UpdateManagedRoleGeneralCommandValidator()
    {
        RuleFor(command => command.RoleId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(RoleDefinition.MaximumNameLength);
        RuleFor(command => command.Description).MaximumLength(RoleDefinition.MaximumDescriptionLength);
        RuleFor(command => command.EditorVersion).NotEmpty().Length(64);
    }
}
