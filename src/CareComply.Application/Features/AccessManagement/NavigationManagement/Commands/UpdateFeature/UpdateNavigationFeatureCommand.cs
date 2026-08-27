using System.Globalization;
using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

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
