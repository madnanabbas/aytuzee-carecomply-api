using CareComply.Application.Abstractions.Navigation;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.UpdateModule;

public sealed class UpdateNavigationModuleCommandHandler(INavigationAdministration navigation)
    : ICommandHandler<UpdateNavigationModuleCommand, UpdateNavigationModuleResult>
{
    public async ValueTask<UpdateNavigationModuleResult> Handle(
        UpdateNavigationModuleCommand command,
        CancellationToken cancellationToken) =>
        UpdateNavigationModuleResult.Succeeded(await navigation.UpdateModuleAsync(
            command.ModuleId,
            command.Key.Trim(),
            command.Name.Trim(),
            command.SortOrder,
            command.IsActive,
            cancellationToken));
}
