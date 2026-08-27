using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Queries.ListNavigation;

public sealed class ListNavigationManagementAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<ListNavigationManagementResult>
{
    public ListNavigationManagementResult Create(AuthorizationFailure failure) =>
        ListNavigationManagementResult.Denied(failure);
}
