using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;
using System.Globalization;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.UpdateFeature;

public sealed record UpdateNavigationFeatureCommand(
    int FeatureId,
    int ModuleId,
    int? ParentFeatureId,
    string Key,
    string Name,
    string Route,
    string IconKey,
    int SortOrder,
    bool IsActive)
    : ICommand<UpdateNavigationFeatureResult>,
      IRequirePermission,
      IAuditableRequest<UpdateNavigationFeatureResult>
{
    public string RequiredPermission => Permissions.Permission.Manage;

    public AuditEvent CreateAuditEvent(UpdateNavigationFeatureResult response) => new(
        "Navigation.FeatureUpdated",
        "NavigationFeature",
        FeatureId.ToString(CultureInfo.InvariantCulture),
        response.Mutation?.Status == NavigationAdministrationMutationStatus.Succeeded
            ? AuditOutcome.Succeeded
            : AuditOutcome.Failed,
        AuditMetadata.Create(("reasonCode", response.ReasonCode)));
}

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

public sealed record UpdateNavigationFeatureResult(
    NavigationAdministrationMutation? Mutation,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<UpdateNavigationFeatureResult>
{
    public string ReasonCode =>
        AuthorizationFailure?.ToString()
        ?? Mutation?.Status.ToString()
        ?? "Unavailable";

    public static UpdateNavigationFeatureResult Succeeded(
        NavigationAdministrationMutation mutation) => new(mutation, null);

    public static UpdateNavigationFeatureResult Denied(
        AuthorizationFailure failure) => new(null, failure);
}
