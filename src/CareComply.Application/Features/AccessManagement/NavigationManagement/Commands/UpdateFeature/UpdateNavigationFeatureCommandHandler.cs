using CareComply.Application.Abstractions.Navigation;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.UpdateFeature;

public sealed class UpdateNavigationFeatureCommandHandler(INavigationAdministration navigation)
    : ICommandHandler<UpdateNavigationFeatureCommand, UpdateNavigationFeatureResult>
{
    public async ValueTask<UpdateNavigationFeatureResult> Handle(
        UpdateNavigationFeatureCommand command,
        CancellationToken cancellationToken) =>
        UpdateNavigationFeatureResult.Succeeded(await navigation.UpdateFeatureAsync(
            command.FeatureId,
            command.ModuleId,
            command.ParentFeatureId,
            command.Key.Trim(),
            command.Name.Trim(),
            command.Route.Trim(),
            command.IconKey.Trim(),
            command.SortOrder,
            command.IsActive,
            cancellationToken));
}
