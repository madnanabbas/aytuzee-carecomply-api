using CareComply.Application.Abstractions.Navigation;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.CreateModule;

public sealed class CreateNavigationModuleCommandHandler(INavigationAdministration navigation)
    : ICommandHandler<CreateNavigationModuleCommand, CreateNavigationModuleResult>
{
    public async ValueTask<CreateNavigationModuleResult> Handle(
        CreateNavigationModuleCommand command,
        CancellationToken cancellationToken) =>
        CreateNavigationModuleResult.Succeeded(await navigation.CreateModuleAsync(
            command.Key.Trim(),
            command.Name.Trim(),
            command.SortOrder,
            command.IsActive,
            cancellationToken));
}
