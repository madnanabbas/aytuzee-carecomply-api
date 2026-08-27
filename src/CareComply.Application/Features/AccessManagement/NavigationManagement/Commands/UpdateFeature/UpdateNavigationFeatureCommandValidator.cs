using CareComply.Application.Features.AccessManagement.NavigationManagement;
using FluentValidation;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.UpdateFeature;

public sealed class UpdateNavigationFeatureCommandValidator
    : AbstractValidator<UpdateNavigationFeatureCommand>
{
    public UpdateNavigationFeatureCommandValidator()
    {
        RuleFor(command => command.FeatureId).GreaterThan(0);
        RuleFor(command => command.ModuleId).GreaterThan(0);
        RuleFor(command => command.ParentFeatureId)
            .GreaterThan(0)
            .When(command => command.ParentFeatureId.HasValue);

        RuleFor(command => command.Key)
            .NotEmpty()
            .MaximumLength(NavigationManagementValidation.MaximumKeyLength)
            .Must(NavigationManagementValidation.IsValidKey)
            .WithMessage("Feature key must be a lowercase slug containing letters, digits, or hyphens.");

        RuleFor(command => command.Name)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Feature name is required.")
            .MaximumLength(NavigationManagementValidation.MaximumNameLength);

        RuleFor(command => command.Route)
            .NotEmpty()
            .MaximumLength(NavigationManagementValidation.MaximumRouteLength)
            .Must(NavigationManagementValidation.IsValidInternalRoute)
            .WithMessage("Feature route must be a safe internal application route.");

        RuleFor(command => command.IconKey)
            .NotEmpty()
            .MaximumLength(NavigationManagementValidation.MaximumIconKeyLength)
            .Must(NavigationManagementValidation.IsValidIconKey)
            .WithMessage("Icon key must be a lowercase slug containing letters, digits, or hyphens.");

        RuleFor(command => command.SortOrder)
            .InclusiveBetween(0, NavigationManagementValidation.MaximumSortOrder);
    }
}
