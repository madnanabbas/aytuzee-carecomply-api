namespace CareComply.Application.Features.Authentication.Queries.GetCurrentUser;

public sealed record CurrentUser(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Permissions);
