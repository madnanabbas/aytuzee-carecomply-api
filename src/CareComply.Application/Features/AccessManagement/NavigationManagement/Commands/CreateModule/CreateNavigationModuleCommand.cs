using System.Globalization;
using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.CreateModule;

public sealed record CreateNavigationModuleCommand(
    string Key,
    string Name,
    int SortOrder,
    bool IsActive)
    : ICommand<CreateNavigationModuleResult>,
      IRequirePermission,
      IAuditableRequest<CreateNavigationModuleResult>
{
    public string RequiredPermission => Permissions.Permission.Manage;

    public AuditEvent CreateAuditEvent(CreateNavigationModuleResult response) => new(
        "Navigation.ModuleCreated",
        "NavigationModule",
        response.Mutation?.Id?.ToString(CultureInfo.InvariantCulture) ?? "unavailable",
        response.Mutation?.Status == NavigationAdministrationMutationStatus.Succeeded
            ? AuditOutcome.Succeeded
            : AuditOutcome.Failed,
        AuditMetadata.Create(("reasonCode", response.ReasonCode)));
}
