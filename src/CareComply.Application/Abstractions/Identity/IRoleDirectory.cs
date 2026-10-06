namespace CareComply.Application.Abstractions.Identity;

public interface IRoleDirectory
{
    Task<IReadOnlyList<RoleDirectoryItem>> ListAsync(CancellationToken cancellationToken);
}

public sealed record RoleDirectoryItem(
    Guid Id,
    string Name,
    IReadOnlyList<string> Permissions,
    bool RequiresMfa,
    Guid CompanyId = default,
    string CompanyName = "");
