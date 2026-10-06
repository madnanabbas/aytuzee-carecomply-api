using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;
using System.Globalization;

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

public sealed record CreateNavigationModuleResult(
    NavigationAdministrationMutation? Mutation,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<CreateNavigationModuleResult>
{
    public string ReasonCode =>
        AuthorizationFailure?.ToString()
        ?? Mutation?.Status.ToString()
        ?? "Unavailable";

    public static CreateNavigationModuleResult Succeeded(
        NavigationAdministrationMutation mutation) => new(mutation, null);

    public static CreateNavigationModuleResult Denied(
        AuthorizationFailure failure) => new(null, failure);
}
