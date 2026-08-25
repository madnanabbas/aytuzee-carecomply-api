using CareComply.Api.Contracts.Navigation;
using CareComply.Application.Features.Navigation.Queries.GetNavigation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/navigation")]
public sealed class NavigationController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NavigationModuleResponse>>> Get(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetNavigationQuery(), cancellationToken);
        if (result.IsUnauthenticated)
        {
            return Unauthorized();
        }

        var modules = result.Modules
            ?? throw new InvalidOperationException(
                "The navigation result does not contain module metadata.");
        return Ok(modules.Select(MapModule));
    }

    private static NavigationModuleResponse MapModule(NavigationModuleResult module) =>
        new(
            module.Key,
            module.Name,
            module.SortOrder,
            module.Features.Select(MapFeature).ToArray());

    private static NavigationFeatureResponse MapFeature(NavigationFeatureResult feature) =>
        new(
            feature.Key,
            feature.Name,
            feature.Route,
            feature.IconKey,
            feature.SortOrder,
            feature.Children.Select(MapFeature).ToArray());
}
