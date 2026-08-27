using CareComply.Api.Authorization;
using CareComply.Api.Contracts.NavigationManagement;
using CareComply.Api.Errors;
using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Authorization;
using CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.CreateFeature;
using CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.CreateModule;
using CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.UpdateFeature;
using CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.UpdateModule;
using CareComply.Application.Features.AccessManagement.NavigationManagement.Queries.ListNavigation;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/access-management/navigation-management")]
public sealed class NavigationManagementController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Permission.View)]
    public async Task<ActionResult<IReadOnlyList<NavigationManagementModuleResponse>>> List(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListNavigationManagementQuery(), cancellationToken);
        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        var modules = result.Modules
            ?? throw new InvalidOperationException(
                "The navigation administration result does not contain module metadata.");

        return Ok(modules.Select(MapModule).ToArray());
    }

    [HttpPost("modules")]
    [HasPermission(Permissions.Permission.Manage)]
    public async Task<ActionResult<NavigationManagementMutationResponse>> CreateModule(
        CreateNavigationModuleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateNavigationModuleCommand(
                request.Key,
                request.Name,
                request.SortOrder,
                request.IsActive),
            cancellationToken);

        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return MapMutation(result.Mutation, created: true);
    }

    [HttpPut("modules/{moduleId:int}")]
    [HasPermission(Permissions.Permission.Manage)]
    public async Task<ActionResult<NavigationManagementMutationResponse>> UpdateModule(
        int moduleId,
        UpdateNavigationModuleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateNavigationModuleCommand(
                moduleId,
                request.Key,
                request.Name,
                request.SortOrder,
                request.IsActive),
            cancellationToken);

        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return MapMutation(result.Mutation, created: false);
    }

    [HttpPost("features")]
    [HasPermission(Permissions.Permission.Manage)]
    public async Task<ActionResult<NavigationManagementMutationResponse>> CreateFeature(
        CreateNavigationFeatureRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateNavigationFeatureCommand(
                request.ModuleId,
                request.ParentFeatureId,
                request.Key,
                request.Name,
                request.Route,
                request.IconKey,
                request.SortOrder,
                request.IsActive),
            cancellationToken);

        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return MapMutation(result.Mutation, created: true);
    }

    [HttpPut("features/{featureId:int}")]
    [HasPermission(Permissions.Permission.Manage)]
    public async Task<ActionResult<NavigationManagementMutationResponse>> UpdateFeature(
        int featureId,
        UpdateNavigationFeatureRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateNavigationFeatureCommand(
                featureId,
                request.ModuleId,
                request.ParentFeatureId,
                request.Key,
                request.Name,
                request.Route,
                request.IconKey,
                request.SortOrder,
                request.IsActive),
            cancellationToken);

        if (result.AuthorizationFailure is { } failure)
        {
            return MapAuthorizationFailure(failure);
        }

        return MapMutation(result.Mutation, created: false);
    }

    private ActionResult<NavigationManagementMutationResponse> MapMutation(
        NavigationAdministrationMutation? mutation,
        bool created) => mutation?.Status switch
    {
        NavigationAdministrationMutationStatus.Succeeded when mutation.Id is { } id =>
            created
                ? StatusCode(StatusCodes.Status201Created, new NavigationManagementMutationResponse(id))
                : Ok(new NavigationManagementMutationResponse(id)),
        NavigationAdministrationMutationStatus.NotFound => Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "The requested navigation resource was not found.",
            type: ProblemTypes.NavigationNotFound),
        NavigationAdministrationMutationStatus.DuplicateKey => Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "The navigation key is already in use.",
            type: ProblemTypes.NavigationDuplicateKey),
        NavigationAdministrationMutationStatus.DuplicateRoute => Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "The navigation route is already in use.",
            type: ProblemTypes.NavigationDuplicateRoute),
        NavigationAdministrationMutationStatus.InvalidParent => Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "The selected parent feature is invalid.",
            type: ProblemTypes.NavigationInvalidParent),
        _ => Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "The navigation configuration could not be changed.",
            type: ProblemTypes.NavigationConflict),
    };

    private ActionResult MapAuthorizationFailure(AuthorizationFailure failure) =>
        failure == AuthorizationFailure.Unauthenticated
            ? Unauthorized()
            : Forbid();

    private static NavigationManagementModuleResponse MapModule(
        NavigationAdministrationModule module) => new(
        module.Id,
        module.Key,
        module.Name,
        module.SortOrder,
        module.IsActive,
        module.Features.Select(MapFeature).ToArray());

    private static NavigationManagementFeatureResponse MapFeature(
        NavigationAdministrationFeature feature) => new(
        feature.Id,
        feature.ModuleId,
        feature.ParentFeatureId,
        feature.Key,
        feature.Name,
        feature.Route,
        feature.IconKey,
        feature.SortOrder,
        feature.IsActive);
}
