namespace CareComply.Application.Abstractions.Identity;

public interface IUserRoleAdministration
{
    Task<UserRoleUpdateResult> ReplaceAsync(
        Guid userId,
        IReadOnlyList<Guid> roleIds,
        string editorVersion,
        CancellationToken cancellationToken);
}

public sealed record UserRoleUpdateResult(
    UserRoleUpdateStatus Status,
    UserDirectoryDetails? User,
    IReadOnlyList<Guid> AddedRoleIds,
    IReadOnlyList<Guid> RemovedRoleIds);

public enum UserRoleUpdateStatus
{
    Succeeded,
    NotFound,
    Conflict,
    RoleNotFound,
    NoChanges,
    Unavailable,
}
