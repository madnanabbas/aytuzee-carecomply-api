using CareComply.Api.Authorization;
using CareComply.Application.Abstractions.Companies;
using CareComply.Application.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace CareComply.Api.Controllers;

[ApiController, Route("api/locations")]
public sealed class LocationsController(ILocationAdministration locations) : ControllerBase
{
    [HttpGet, Authorize]
    public async Task<ActionResult<IReadOnlyList<CompanyLocationItem>>> List([FromQuery] Guid? companyId, CancellationToken ct) => Ok(await locations.ListAsync(companyId, ct));
    [HttpPost, HasPermission(Permissions.Company.Edit)]
    public async Task<ActionResult<CompanyLocationItem>> Create(CreateLocationRequest request, CancellationToken ct) =>
        await locations.CreateAsync(request.CompanyId, request.Name, ct) is { } item ? Ok(item) : BadRequest();
}
public sealed record CreateLocationRequest(Guid? CompanyId, string Name);
