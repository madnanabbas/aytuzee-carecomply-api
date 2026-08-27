using System.Globalization;
using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

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
