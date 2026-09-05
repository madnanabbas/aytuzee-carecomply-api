using System.Globalization;
using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.UpdateFeature;
using CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.UpdateModule;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.DeleteNavigation;

public sealed record DeleteNavigationModuleCommand(int ModuleId)
    : ICommand<UpdateNavigationModuleResult>, IRequirePermission, IAuditableRequest<UpdateNavigationModuleResult>
{
    public string RequiredPermission => Permissions.Permission.Manage;
    public AuditEvent CreateAuditEvent(UpdateNavigationModuleResult response) => new(
        "Navigation.ModuleDeleted", "NavigationModule", ModuleId.ToString(CultureInfo.InvariantCulture),
        response.Mutation?.Status == NavigationAdministrationMutationStatus.Succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
        AuditMetadata.Create(("reasonCode", response.ReasonCode)));
}

public sealed record DeleteNavigationFeatureCommand(int FeatureId)
    : ICommand<UpdateNavigationFeatureResult>, IRequirePermission, IAuditableRequest<UpdateNavigationFeatureResult>
{
    public string RequiredPermission => Permissions.Permission.Manage;
    public AuditEvent CreateAuditEvent(UpdateNavigationFeatureResult response) => new(
        "Navigation.FeatureDeleted", "NavigationFeature", FeatureId.ToString(CultureInfo.InvariantCulture),
        response.Mutation?.Status == NavigationAdministrationMutationStatus.Succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
        AuditMetadata.Create(("reasonCode", response.ReasonCode)));
}

public sealed class DeleteNavigationModuleCommandHandler(INavigationAdministration navigation)
    : ICommandHandler<DeleteNavigationModuleCommand, UpdateNavigationModuleResult>
{
    public async ValueTask<UpdateNavigationModuleResult> Handle(DeleteNavigationModuleCommand command, CancellationToken cancellationToken) =>
        UpdateNavigationModuleResult.Succeeded(await navigation.DeleteModuleAsync(command.ModuleId, cancellationToken));
}

public sealed class DeleteNavigationFeatureCommandHandler(INavigationAdministration navigation)
    : ICommandHandler<DeleteNavigationFeatureCommand, UpdateNavigationFeatureResult>
{
    public async ValueTask<UpdateNavigationFeatureResult> Handle(DeleteNavigationFeatureCommand command, CancellationToken cancellationToken) =>
        UpdateNavigationFeatureResult.Succeeded(await navigation.DeleteFeatureAsync(command.FeatureId, cancellationToken));
}
