namespace CareComply.Api.Contracts.AccessManagement;

public sealed record RoleMetadataResponse(
    Guid Id,
    string Name,
    IReadOnlyList<string> Permissions,
    bool RequiresMfa);
