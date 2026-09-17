namespace CareComply.Api.Contracts.AccessManagement;

public sealed record RoleMetadataResponse(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string Name,
    IReadOnlyList<string> Permissions,
    bool RequiresMfa);
