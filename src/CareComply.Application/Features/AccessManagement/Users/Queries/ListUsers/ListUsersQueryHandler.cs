using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.Users.Queries.ListUsers;

public sealed class ListUsersQueryHandler(IUserDirectory userDirectory)
    : IQueryHandler<ListUsersQuery, ListUsersResult>
{
    public async ValueTask<ListUsersResult> Handle(
        ListUsersQuery query,
        CancellationToken cancellationToken)
    {
        var request = new UserDirectoryRequest(
            NormalizeSearch(query.Search),
            query.IsActive,
            query.RoleId,
            ParseMfaStatus(query.MfaStatus),
            ParseSort(query.SortBy),
            string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase),
            query.PageNumber,
            query.PageSize);

        var page = await userDirectory.ListAsync(request, cancellationToken);
        return ListUsersResult.Succeeded(page);
    }

    private static string? NormalizeSearch(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search.Trim();

    private static UserDirectoryMfaFilter? ParseMfaStatus(string? status) =>
        status?.ToUpperInvariant() switch
        {
            "ENABLED" => UserDirectoryMfaFilter.Enabled,
            "NOTENABLED" => UserDirectoryMfaFilter.NotEnabled,
            _ => null,
        };

    private static UserDirectorySort ParseSort(string? sort) =>
        sort?.ToUpperInvariant() switch
        {
            "EMAIL" => UserDirectorySort.Email,
            "STATUS" => UserDirectorySort.Status,
            "MFA" => UserDirectorySort.Mfa,
            _ => UserDirectorySort.Name,
        };
}
