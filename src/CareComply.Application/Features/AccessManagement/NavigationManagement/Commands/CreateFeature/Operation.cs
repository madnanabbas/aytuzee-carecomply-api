using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;
using System.Globalization;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.CreateFeature;

public sealed record CreateNavigationFeatureCommand(
    int ModuleId,
    int? ParentFeatureId,
    string Key,
    string Name,
    string Route,
    string IconKey,
    int SortOrder,
    bool IsActive)
    : ICommand<CreateNavigationFeatureResult>,
      IRequirePermission,
      IAuditableRequest<CreateNavigationFeatureResult>
{
    public string RequiredPermission => Permissions.Permission.Manage;

    public AuditEvent CreateAuditEvent(CreateNavigationFeatureResult response) => new(
        "Navigation.FeatureCreated",
        "NavigationFeature",
        response.Mutation?.Id?.ToString(CultureInfo.InvariantCulture) ?? "unavailable",
        response.Mutation?.Status == NavigationAdministrationMutationStatus.Succeeded
            ? AuditOutcome.Succeeded
            : AuditOutcome.Failed,
        AuditMetadata.Create(("reasonCode", response.ReasonCode)));
}

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

public sealed record CreateNavigationFeatureResult(
    NavigationAdministrationMutation? Mutation,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<CreateNavigationFeatureResult>
{
    public string ReasonCode =>
        AuthorizationFailure?.ToString()
        ?? Mutation?.Status.ToString()
        ?? "Unavailable";

    public static CreateNavigationFeatureResult Succeeded(
        NavigationAdministrationMutation mutation) => new(mutation, null);

    public static CreateNavigationFeatureResult Denied(
        AuthorizationFailure failure) => new(null, failure);
}
