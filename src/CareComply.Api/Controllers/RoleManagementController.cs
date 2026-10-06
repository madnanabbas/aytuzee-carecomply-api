using CareComply.Api.Authorization;
using CareComply.Api.Contracts.AccessManagement;
using CareComply.Api.Errors;
using CareComply.Application.Abstractions.AccessManagement;
using CareComply.Application.Authorization;
using CareComply.Application.Features.AccessManagement.RoleManagement.Commands.CreateRole;
using CareComply.Application.Features.AccessManagement.RoleManagement.Commands.ReplaceRolePermissions;
using CareComply.Application.Features.AccessManagement.RoleManagement.Commands.UpdateRoleGeneral;
using CareComply.Application.Features.AccessManagement.RoleManagement.Queries.GetPermissionCatalog;
using CareComply.Application.Features.AccessManagement.RoleManagement.Queries.GetRole;
using CareComply.Application.Features.AccessManagement.RoleManagement.Queries.ListRoles;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/access-management/role-management")]
public sealed class RoleManagementController(ISender sender) : ControllerBase
{
    [HttpGet("roles")]
    [HasPermission(Permissions.Role.View)]
    public async Task<ActionResult<IReadOnlyList<ManagedRoleListItemResponse>>> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListManagedRolesQuery(), cancellationToken);
        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return Ok((result.Roles ?? []).Select(role => new ManagedRoleListItemResponse(
            role.Id, role.CompanyId, role.CompanyName, role.Name, role.Description, role.AssignedUserCount, role.PermissionCount, role.EditorVersion)));
    }

    [HttpGet("roles/{roleId:guid}")]
    [HasPermission(Permissions.Role.View)]
    public async Task<ActionResult<ManagedRoleDetailsResponse>> Get(Guid roleId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetManagedRoleQuery(roleId), cancellationToken);
        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return result.Role is { } role
            ? Ok(MapRole(role))
            : Problem(statusCode: 404, title: "Role not found.", type: ProblemTypes.RoleNotFound);
    }

    [HttpGet("permission-catalog")]
    [HasPermission(Permissions.Role.View)]
    public async Task<ActionResult<IReadOnlyList<RolePermissionCatalogModuleResponse>>> PermissionCatalog(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRolePermissionCatalogQuery(), cancellationToken);
        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return Ok((result.Modules ?? []).Select(module => new RolePermissionCatalogModuleResponse(
            module.Id,
            module.Key,
            module.Name,
            module.SortOrder,
            module.Features.Select(feature => new RolePermissionCatalogFeatureResponse(
                feature.Id,
                feature.Key,
                feature.Name,
                feature.SortOrder,
                feature.Permissions.Select(permission => new RolePermissionCatalogPermissionResponse(
                    permission.Id,
                    permission.Code,
                    permission.Action)).ToArray())).ToArray())));
    }

    [HttpPost("roles")]
    [HasPermission(Permissions.Role.Manage)]
    public async Task<ActionResult<ManagedRoleDetailsResponse>> Create(
        CreateManagedRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateManagedRoleCommand(
            request.CompanyId,
            request.Name,
            request.Description,
            request.PermissionIds ?? []), cancellationToken);
        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return result.Mutation?.Status switch
        {
            RoleMutationStatus.Succeeded when result.Mutation.Role is { } role =>
                CreatedAtAction(nameof(Get), new { roleId = role.Id }, MapRole(role)),
            RoleMutationStatus.DuplicateName => Problem(statusCode: 409, title: "A role with that name already exists.", type: ProblemTypes.RoleDuplicateName),
            RoleMutationStatus.PermissionNotFound => Problem(statusCode: 400, title: "One or more permissions are unavailable.", type: ProblemTypes.RolePermissionInvalid),
            _ => Problem(statusCode: 409, title: "The role could not be created.", type: ProblemTypes.RoleConflict),
        };
    }

    [HttpPut("roles/{roleId:guid}/general")]
    [HasPermission(Permissions.Role.Manage)]
    public async Task<ActionResult<ManagedRoleDetailsResponse>> UpdateGeneral(
        Guid roleId,
        UpdateManagedRoleGeneralRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateManagedRoleGeneralCommand(
            roleId, request.Name, request.Description, request.EditorVersion), cancellationToken);
        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return MapMutation(result.Mutation);
    }

    [HttpPut("roles/{roleId:guid}/permissions")]
    [HasPermission(Permissions.Role.Manage)]
    public async Task<ActionResult<ManagedRoleDetailsResponse>> ReplacePermissions(
        Guid roleId,
        ReplaceManagedRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ReplaceManagedRolePermissionsCommand(
            roleId, request.PermissionIds ?? [], request.EditorVersion), cancellationToken);
        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return result.Mutation?.Status == RoleMutationStatus.LastRoleManager
            ? Problem(statusCode: 409, title: "Role.Manage cannot be removed from the last active role-management path.", type: ProblemTypes.RoleLastManager)
            : MapMutation(result.Mutation is null ? null : new RoleMutationResult(result.Mutation.Status, result.Mutation.Role));
    }

    private ActionResult<ManagedRoleDetailsResponse> MapMutation(RoleMutationResult? mutation) => mutation?.Status switch
    {
        RoleMutationStatus.Succeeded or RoleMutationStatus.NoChanges when mutation.Role is { } role => Ok(MapRole(role)),
        RoleMutationStatus.NotFound => Problem(statusCode: 404, title: "Role not found.", type: ProblemTypes.RoleNotFound),
        RoleMutationStatus.DuplicateName => Problem(statusCode: 409, title: "A role with that name already exists.", type: ProblemTypes.RoleDuplicateName),
        RoleMutationStatus.PermissionNotFound => Problem(statusCode: 400, title: "One or more permissions are unavailable.", type: ProblemTypes.RolePermissionInvalid),
        _ => Problem(statusCode: 409, title: "The role was changed by another request. Reload and try again.", type: ProblemTypes.RoleConflict),
    };

    private ActionResult MapAuthorizationFailure(AuthorizationFailure failure) =>
        failure == AuthorizationFailure.Unauthenticated ? Unauthorized() : Forbid();

    private static ManagedRoleDetailsResponse MapRole(RoleManagementDetails role) => new(
        role.Id,
        role.CompanyId,
        role.CompanyName,
        role.Name,
        role.Description,
        role.PermissionIds,
        role.AssignedUserCount,
        role.EditorVersion);
}
