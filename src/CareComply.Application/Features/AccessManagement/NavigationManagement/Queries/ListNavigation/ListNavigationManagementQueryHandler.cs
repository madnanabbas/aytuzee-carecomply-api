using CareComply.Application.Abstractions.Navigation;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Queries.ListNavigation;

public sealed class ListNavigationManagementQueryHandler(INavigationAdministration navigation)
    : IQueryHandler<ListNavigationManagementQuery, ListNavigationManagementResult>
{
    public async ValueTask<ListNavigationManagementResult> Handle(
        ListNavigationManagementQuery query,
        CancellationToken cancellationToken) =>
        ListNavigationManagementResult.Succeeded(
            await navigation.ListAsync(cancellationToken));
}
