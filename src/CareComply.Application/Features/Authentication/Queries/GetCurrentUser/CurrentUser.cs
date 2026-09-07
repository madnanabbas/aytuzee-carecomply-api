namespace CareComply.Application.Features.Authentication.Queries.GetCurrentUser;

public sealed record CurrentUser(
    Guid Id,
    string Email,
    string DisplayName,
    Guid CompanyId,
    string CompanyName,
    bool IsPlatformAdministrator,
    IReadOnlyList<string> Permissions);
