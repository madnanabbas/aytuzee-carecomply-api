using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Queries.ListNavigation;

public sealed record ListNavigationManagementQuery
    : IQuery<ListNavigationManagementResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.Permission.View;
}

public sealed class ListNavigationManagementQueryHandler(INavigationAdministration navigation)
    : IQueryHandler<ListNavigationManagementQuery, ListNavigationManagementResult>
{
    public async ValueTask<ListNavigationManagementResult> Handle(
        ListNavigationManagementQuery query,
        CancellationToken cancellationToken) =>
        ListNavigationManagementResult.Succeeded(
            await navigation.ListAsync(cancellationToken));
}

public sealed record ListNavigationManagementResult(
    IReadOnlyList<NavigationAdministrationModule>? Modules,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<ListNavigationManagementResult>
{
    public static ListNavigationManagementResult Succeeded(
        IReadOnlyList<NavigationAdministrationModule> modules) => new(modules, null);

    public static ListNavigationManagementResult Denied(
        AuthorizationFailure failure) => new(null, failure);
}
