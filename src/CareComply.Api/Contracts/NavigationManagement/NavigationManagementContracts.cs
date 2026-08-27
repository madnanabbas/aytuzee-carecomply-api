namespace CareComply.Api.Contracts.NavigationManagement;

public sealed record NavigationManagementModuleResponse(
    int Id,
    string Key,
    string Name,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<NavigationManagementFeatureResponse> Features);

public sealed record NavigationManagementFeatureResponse(
    int Id,
    int ModuleId,
    int? ParentFeatureId,
    string Key,
    string Name,
    string Route,
    string IconKey,
    int SortOrder,
    bool IsActive);

public sealed record CreateNavigationModuleRequest(
    string Key,
    string Name,
    int SortOrder,
    bool IsActive);

public sealed record UpdateNavigationModuleRequest(
    string Key,
    string Name,
    int SortOrder,
    bool IsActive);

public sealed record CreateNavigationFeatureRequest(
    int ModuleId,
    int? ParentFeatureId,
    string Key,
    string Name,
    string Route,
    string IconKey,
    int SortOrder,
    bool IsActive);

public sealed record UpdateNavigationFeatureRequest(
    int ModuleId,
    int? ParentFeatureId,
    string Key,
    string Name,
    string Route,
    string IconKey,
    int SortOrder,
    bool IsActive);

public sealed record NavigationManagementMutationResponse(int Id);
