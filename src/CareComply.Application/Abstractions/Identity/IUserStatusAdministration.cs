namespace CareComply.Application.Abstractions.Identity;

public interface IUserStatusAdministration
{
    Task<UserStatusUpdateResult> SetActiveAsync(
        Guid userId,
        bool isActive,
        string editorVersion,
        CancellationToken cancellationToken);
}

public sealed record UserStatusUpdateResult(
    UserAdministrationStatus Status,
    UserDirectoryDetails? User);
