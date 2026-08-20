namespace CareComply.Api.Contracts.Authentication;

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Permissions);
