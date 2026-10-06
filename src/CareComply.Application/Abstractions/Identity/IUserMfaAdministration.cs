namespace CareComply.Application.Abstractions.Identity;

public interface IUserMfaAdministration
{
    Task<UserMfaUpdateResult> SetAsync(
        Guid userId,
        bool isEnabled,
        UserMfaMethod method,
        string editorVersion,
        CancellationToken cancellationToken);
}

public enum UserMfaUpdateStatus
{
    Succeeded,
    MethodChanged,
    NoChanges,
    NotFound,
    Conflict,
    RequiredByPermissions,
    Unavailable,
}

public sealed record UserMfaUpdateResult(
    UserMfaUpdateStatus Status,
    UserDirectoryDetails? User);
