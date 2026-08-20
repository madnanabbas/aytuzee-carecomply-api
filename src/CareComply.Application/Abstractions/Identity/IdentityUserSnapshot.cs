namespace CareComply.Application.Abstractions.Identity;

public sealed record IdentityUserSnapshot(
    Guid Id,
    string Email,
    string DisplayName);
