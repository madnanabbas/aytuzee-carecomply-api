using CareComply.Application.Abstractions.Navigation;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.CreateFeature;

public sealed class CreateNavigationFeatureCommandHandler(INavigationAdministration navigation)
    : ICommandHandler<CreateNavigationFeatureCommand, CreateNavigationFeatureResult>
{
    public async ValueTask<CreateNavigationFeatureResult> Handle(
        CreateNavigationFeatureCommand command,
        CancellationToken cancellationToken) =>
        CreateNavigationFeatureResult.Succeeded(await navigation.CreateFeatureAsync(
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
