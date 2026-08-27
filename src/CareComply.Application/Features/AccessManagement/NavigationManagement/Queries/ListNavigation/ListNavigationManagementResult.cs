using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Queries.ListNavigation;

public sealed record ListNavigationManagementResult(
    IReadOnlyList<NavigationAdministrationModule>? Modules,
    AuthorizationFailure? AuthorizationFailure)
{
    public static ListNavigationManagementResult Succeeded(
        IReadOnlyList<NavigationAdministrationModule> modules) => new(modules, null);

    public static ListNavigationManagementResult Denied(
        AuthorizationFailure failure) => new(null, failure);
}
