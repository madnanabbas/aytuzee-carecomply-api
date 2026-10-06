using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;
using System.Globalization;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.UpdateModule;

public sealed record UpdateNavigationModuleCommand(
    int ModuleId,
    string Key,
    string Name,
    int SortOrder,
    bool IsActive)
    : ICommand<UpdateNavigationModuleResult>,
      IRequirePermission,
      IAuditableRequest<UpdateNavigationModuleResult>
{
    public string RequiredPermission => Permissions.Permission.Manage;

    public AuditEvent CreateAuditEvent(UpdateNavigationModuleResult response) => new(
        "Navigation.ModuleUpdated",
        "NavigationModule",
        ModuleId.ToString(CultureInfo.InvariantCulture),
        response.Mutation?.Status == NavigationAdministrationMutationStatus.Succeeded
            ? AuditOutcome.Succeeded
            : AuditOutcome.Failed,
        AuditMetadata.Create(("reasonCode", response.ReasonCode)));
}

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

public sealed record UpdateNavigationModuleResult(
    NavigationAdministrationMutation? Mutation,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<UpdateNavigationModuleResult>
{
    public string ReasonCode =>
        AuthorizationFailure?.ToString()
        ?? Mutation?.Status.ToString()
        ?? "Unavailable";

    public static UpdateNavigationModuleResult Succeeded(
        NavigationAdministrationMutation mutation) => new(mutation, null);

    public static UpdateNavigationModuleResult Denied(
        AuthorizationFailure failure) => new(null, failure);
}
