namespace CareComply.Application.Abstractions.Identity;

public interface IUserDirectory
{
    Task<UserDirectoryPage> ListAsync(
        UserDirectoryRequest request,
        CancellationToken cancellationToken);

    Task<UserDirectoryDetails?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record UserDirectoryDetails(
    Guid Id,
    string FirstName,
    string LastName,
    string DisplayName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    bool IsMfaEnabled,
    bool RequiresMfa,
    bool IsMfaRequired,
    bool IsMfaRequiredByPermissions,
    UserMfaMethod MfaMethod,
    string EditorVersion)
{
    public IReadOnlyList<UserDirectoryRole> Roles { get; init; } = [];
}

public sealed record UserDirectoryRequest(
    string? Search,
    bool? IsActive,
    Guid? RoleId,
    UserDirectoryMfaFilter? MfaStatus,
    UserDirectorySort Sort,
    bool SortDescending,
    int PageNumber,
    int PageSize);

public enum UserDirectoryMfaFilter
{
    Enabled,
    NotEnabled,
}

public enum UserDirectorySort
{
    Name,
    Email,
    Status,
    Mfa,
}

public sealed record UserDirectoryPage(
    IReadOnlyList<UserDirectoryItem> Items,
    IReadOnlyList<UserDirectoryRole> AvailableRoles,
    int PageNumber,
    int PageSize,
    int TotalCount);

public sealed record UserDirectoryItem(
    Guid Id,
    string DisplayName,
    string Email,
    bool IsActive,
    bool IsMfaEnabled,
    IReadOnlyList<UserDirectoryRole> Roles);

public sealed record UserDirectoryRole(Guid Id, string Name);
