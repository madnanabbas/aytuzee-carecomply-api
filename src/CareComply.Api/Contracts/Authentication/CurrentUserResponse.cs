namespace CareComply.Api.Contracts.Authentication;

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    Guid? CompanyId,
    string? CompanyName,
    bool IsPlatformAdministrator,
    IReadOnlyList<string> Permissions);
