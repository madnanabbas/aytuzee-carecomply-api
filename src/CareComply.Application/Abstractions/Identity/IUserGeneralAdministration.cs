namespace CareComply.Application.Abstractions.Identity;

public interface IUserGeneralAdministration
{
    Task<UserGeneralUpdateResult> UpdateAsync(
        UserGeneralUpdateRequest request,
        CancellationToken cancellationToken);
}

public sealed record UserGeneralUpdateRequest(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string EditorVersion);

public sealed record UserGeneralUpdateResult(
    UserAdministrationStatus Status,
    UserDirectoryDetails? User,
    IReadOnlyList<string> ChangedFields);

public enum UserAdministrationStatus
{
    Succeeded,
    NotFound,
    Conflict,
    DuplicateEmail,
    NoChanges,
}
