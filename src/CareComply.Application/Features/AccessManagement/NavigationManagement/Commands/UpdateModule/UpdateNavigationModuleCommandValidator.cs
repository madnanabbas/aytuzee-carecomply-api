using CareComply.Application.Features.AccessManagement.NavigationManagement;
using FluentValidation;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.UpdateModule;

public sealed class UpdateNavigationModuleCommandValidator
    : AbstractValidator<UpdateNavigationModuleCommand>
{
    public UpdateNavigationModuleCommandValidator()
    {
        RuleFor(command => command.ModuleId).GreaterThan(0);

        RuleFor(command => command.Key)
            .NotEmpty()
            .MaximumLength(NavigationManagementValidation.MaximumKeyLength)
            .Must(NavigationManagementValidation.IsValidKey)
            .WithMessage("Module key must be a lowercase slug containing letters, digits, or hyphens.");

        RuleFor(command => command.Name)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Module name is required.")
            .MaximumLength(NavigationManagementValidation.MaximumNameLength);

        RuleFor(command => command.SortOrder)
            .InclusiveBetween(0, NavigationManagementValidation.MaximumSortOrder);
    }
}
