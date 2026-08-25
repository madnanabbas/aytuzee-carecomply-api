using CareComply.Api.Authorization;
using CareComply.Api.Contracts.AccessManagement;
using CareComply.Application.Authorization;
using CareComply.Application.Features.AccessManagement.Users.Queries.ListUsers;
using CareComply.Application.Features.AccessManagement.Users.Queries.GetUser;
using CareComply.Application.Features.AccessManagement.Users.Commands.UpdateUserGeneral;
using CareComply.Application.Features.AccessManagement.Users.Commands.SetUserStatus;
using CareComply.Application.Features.AccessManagement.Users.Commands.SetUserMfa;
using CareComply.Application.Features.AccessManagement.Users.Commands.CreateUser;
using CareComply.Application.Features.AccessManagement.Users.Commands.ReplaceUserRoles;
using CareComply.Application.Abstractions.Identity;
using CareComply.Api.Errors;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/access-management/users")]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [HttpPost]
    [HasPermission(Permissions.User.Manage)]
    public async Task<ActionResult<CreateUserResponse>> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseMfaMethod(request.MfaMethod, out var mfaMethod))
        {
            return InvalidMfaMethod();
        }

        var result = await sender.Send(new CreateUserCommand(
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber,
            request.IsActive,
            request.RequiresMfa,
            mfaMethod,
            request.RoleIds ?? []), cancellationToken);
        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return result.Provisioning?.Status switch
        {
            UserProvisioningStatus.Succeeded when result.Provisioning.UserId is { } userId =>
                CreatedAtAction(nameof(Get), new { userId }, new CreateUserResponse(userId)),
            UserProvisioningStatus.DuplicateEmail => Problem(
                statusCode: 409,
                title: "That email address is already in use.",
                type: ProblemTypes.UserDuplicateEmail),
            UserProvisioningStatus.RoleNotFound => Problem(
                statusCode: 400,
                title: "One or more selected roles are unavailable.",
                type: ProblemTypes.UserRoleInvalid),
            _ => Problem(
                statusCode: 409,
                title: "The user could not be created.",
                type: ProblemTypes.UserProvisioningFailed),
        };
    }

    [HttpGet("{userId:guid}", Name = nameof(Get))]
    [HasPermission(Permissions.User.View)]
    public async Task<ActionResult<UserDetailsResponse>> Get(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetUserQuery(userId), cancellationToken);
        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return result.User is { } user
            ? Ok(MapDetails(user))
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found.", type: ProblemTypes.UserNotFound);
    }

    [HttpPut("{userId:guid}/general")]
    [HasPermission(Permissions.User.Manage)]
    public async Task<ActionResult<UserDetailsResponse>> UpdateGeneral(
        Guid userId,
        UpdateUserGeneralRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateUserGeneralCommand(
            userId,
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber,
            request.EditorVersion), cancellationToken);
        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return MapUpdate(result.Update);
    }

    [HttpPost("{userId:guid}/activate")]
    [HasPermission(Permissions.User.Manage)]
    public Task<ActionResult<UserDetailsResponse>> Activate(
        Guid userId,
        UpdateUserStatusRequest request,
        CancellationToken cancellationToken) =>
        SetStatus(userId, true, request.EditorVersion, cancellationToken);

    [HttpPost("{userId:guid}/deactivate")]
    [HasPermission(Permissions.User.Manage)]
    public Task<ActionResult<UserDetailsResponse>> Deactivate(
        Guid userId,
        UpdateUserStatusRequest request,
        CancellationToken cancellationToken) =>
        SetStatus(userId, false, request.EditorVersion, cancellationToken);

    [HttpPut("{userId:guid}/mfa")]
    [HasPermission(Permissions.User.Manage)]
    public async Task<ActionResult<UserDetailsResponse>> SetMfa(
        Guid userId,
        UpdateUserMfaRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseMfaMethod(request.Method, out var mfaMethod))
        {
            return InvalidMfaMethod();
        }

        var result = await sender.Send(
            new SetUserMfaCommand(
                userId,
                request.IsEnabled,
                mfaMethod,
                request.EditorVersion),
            cancellationToken);
        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return result.Update?.Status switch
        {
            UserMfaUpdateStatus.Succeeded
                or UserMfaUpdateStatus.MethodChanged
                or UserMfaUpdateStatus.NoChanges
                when result.Update.User is { } user => Ok(MapDetails(user)),
            UserMfaUpdateStatus.NotFound => Problem(statusCode: 404, title: "User not found.", type: ProblemTypes.UserNotFound),
            UserMfaUpdateStatus.RequiredByPermissions => Problem(
                statusCode: 409,
                title: "MFA is required by this user's permissions and cannot be disabled.",
                type: ProblemTypes.UserMfaRequiredByPermissions),
            UserMfaUpdateStatus.Unavailable => Problem(statusCode: 409, title: "The MFA setting could not be changed.", type: ProblemTypes.UserConflict),
            _ => Problem(statusCode: 409, title: "The user was changed by another request. Reload and try again.", type: ProblemTypes.UserConflict),
        };
    }

    [HttpPut("{userId:guid}/roles")]
    [HasPermission(Permissions.User.Manage)]
    public async Task<ActionResult<UserDetailsResponse>> ReplaceRoles(
        Guid userId,
        UpdateUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ReplaceUserRolesCommand(
                userId,
                request.RoleIds ?? [],
                request.EditorVersion),
            cancellationToken);
        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return result.Update?.Status switch
        {
            UserRoleUpdateStatus.Succeeded or UserRoleUpdateStatus.NoChanges
                when result.Update.User is { } user => Ok(MapDetails(user)),
            UserRoleUpdateStatus.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "User not found.",
                type: ProblemTypes.UserNotFound),
            UserRoleUpdateStatus.RoleNotFound => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "One or more selected roles are unavailable.",
                type: ProblemTypes.UserRoleInvalid),
            _ => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The user was changed by another request. Reload and try again.",
                type: ProblemTypes.UserConflict),
        };
    }

    [HttpGet]
    [HasPermission(Permissions.User.View)]
    public async Task<ActionResult<UserListResponse>> List(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] Guid? roleId,
        [FromQuery] string? mfaStatus,
        [FromQuery] string? sortBy = "name",
        [FromQuery] string? sortDirection = "asc",
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new ListUsersQuery(
                search,
                isActive,
                roleId,
                mfaStatus,
                sortBy,
                sortDirection,
                pageNumber,
                pageSize),
            cancellationToken);

        if (result.AuthorizationFailure is { } failure)
        {
            return failure == AuthorizationFailure.Unauthenticated
                ? Unauthorized()
                : Forbid();
        }

        var page = result.Page
            ?? throw new InvalidOperationException("The user list result does not contain a page.");

        return Ok(new UserListResponse(
            page.Items.Select(item => new UserListItemResponse(
                item.Id,
                item.DisplayName,
                item.Email,
                item.IsActive,
                item.IsMfaEnabled ? "enabled" : "notEnabled",
                item.Roles.Select(MapRole).ToArray())).ToArray(),
            page.AvailableRoles.Select(MapRole).ToArray(),
            page.PageNumber,
            page.PageSize,
            page.TotalCount));
    }

    private static UserListRoleResponse MapRole(
        CareComply.Application.Abstractions.Identity.UserDirectoryRole role) =>
        new(role.Id, role.Name);

    private async Task<ActionResult<UserDetailsResponse>> SetStatus(
        Guid userId,
        bool isActive,
        string editorVersion,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SetUserStatusCommand(userId, isActive, editorVersion),
            cancellationToken);
        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        if (result.SelfDeactivationDenied)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The current user cannot deactivate their own account.",
                type: ProblemTypes.UserSelfDeactivation);
        }

        return MapUpdate(result.Update);
    }

    private ActionResult<UserDetailsResponse> MapUpdate(UserGeneralUpdateResult? result) =>
        result?.Status switch
        {
            UserAdministrationStatus.Succeeded or UserAdministrationStatus.NoChanges
                when result.User is { } user => Ok(MapDetails(user)),
            UserAdministrationStatus.NotFound => Problem(statusCode: 404, title: "User not found.", type: ProblemTypes.UserNotFound),
            UserAdministrationStatus.DuplicateEmail => Problem(statusCode: 409, title: "That email address is already in use.", type: ProblemTypes.UserDuplicateEmail),
            _ => Problem(statusCode: 409, title: "The user was changed by another request. Reload and try again.", type: ProblemTypes.UserConflict),
        };

    private ActionResult<UserDetailsResponse> MapUpdate(UserStatusUpdateResult? result) =>
        result?.Status switch
        {
            UserAdministrationStatus.Succeeded or UserAdministrationStatus.NoChanges
                when result.User is { } user => Ok(MapDetails(user)),
            UserAdministrationStatus.NotFound => Problem(statusCode: 404, title: "User not found.", type: ProblemTypes.UserNotFound),
            _ => Problem(statusCode: 409, title: "The user was changed by another request. Reload and try again.", type: ProblemTypes.UserConflict),
        };

    private ActionResult MapAuthorizationFailure(AuthorizationFailure failure) =>
        failure == AuthorizationFailure.Unauthenticated ? Unauthorized() : Forbid();

    private static UserDetailsResponse MapDetails(UserDirectoryDetails user) => new(
        user.Id,
        user.FirstName,
        user.LastName,
        user.DisplayName,
        user.Email,
        user.PhoneNumber,
        user.IsActive,
        user.IsMfaEnabled,
        user.RequiresMfa,
        user.IsMfaRequired,
        user.IsMfaRequiredByPermissions,
        user.MfaMethod.ToString(),
        user.Roles.Select(MapRole).ToArray(),
        user.EditorVersion);

    private ObjectResult InvalidMfaMethod() => Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "The MFA method is invalid.",
        type: ProblemTypes.UserMfaMethodInvalid);

    private static bool TryParseMfaMethod(
        string? value,
        out UserMfaMethod method)
    {
        method = value switch
        {
            nameof(UserMfaMethod.Authenticator) => UserMfaMethod.Authenticator,
            nameof(UserMfaMethod.Email) => UserMfaMethod.Email,
            _ => default,
        };
        return value is nameof(UserMfaMethod.Authenticator) or nameof(UserMfaMethod.Email);
    }
}
