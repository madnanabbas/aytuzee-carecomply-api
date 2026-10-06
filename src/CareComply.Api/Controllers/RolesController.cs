using CareComply.Api.Authorization;
using CareComply.Api.Contracts.AccessManagement;
using CareComply.Application.Authorization;
using CareComply.Application.Features.AccessManagement.Roles.Queries.ListRoles;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/access-management/roles")]
public sealed class RolesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.User.Manage)]
    public async Task<ActionResult<IReadOnlyList<RoleMetadataResponse>>> List(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListRolesQuery(), cancellationToken);
        if (result.AuthorizationFailure is { } failure)
        {
            return failure == AuthorizationFailure.Unauthenticated
                ? Unauthorized()
                : Forbid();
        }

        var roles = result.Roles
            ?? throw new InvalidOperationException("The role list result does not contain roles.");
        return Ok(roles.Select(role => new RoleMetadataResponse(
            role.Id,
            role.CompanyId,
            role.CompanyName,
            role.Name,
            role.Permissions,
            role.RequiresMfa)));
    }
}
