using System.Globalization;
using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

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
