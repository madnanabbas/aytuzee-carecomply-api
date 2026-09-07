using CareComply.Api.Authorization;
using CareComply.Api.Contracts.Companies;
using CareComply.Application.Abstractions.Companies;
using CareComply.Application.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/companies")]
public sealed class CompaniesController(ICompanyAdministration companies) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Company.View)]
    public async Task<ActionResult<IReadOnlyList<CompanyResponse>>> List(CancellationToken ct) =>
        Ok((await companies.ListAsync(ct)).Select(Map).ToArray());

    [HttpGet("{id:guid}"), HasPermission(Permissions.Company.View)]
    public async Task<ActionResult<CompanyResponse>> Get(Guid id, CancellationToken ct) =>
        await companies.GetAsync(id, ct) is { } company ? Ok(Map(company)) : NotFound();

    [HttpPost, HasPermission(Permissions.Company.Create)]
    public async Task<ActionResult<CompanyResponse>> Create(CreateCompanyRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code)) return ValidationProblem();
        var result = await companies.CreateAsync(request.Name, request.Code, ct);
        return result.Status switch
        {
            CompanyMutationStatus.Succeeded => CreatedAtAction(nameof(Get), new { id = result.Company!.Id }, Map(result.Company)),
            CompanyMutationStatus.DuplicateCode => Conflict(new ProblemDetails { Title = "Company code already exists." }),
            CompanyMutationStatus.Forbidden => Forbid(),
            _ => Conflict()
        };
    }

    [HttpPut("{id:guid}"), HasPermission(Permissions.Company.Edit)]
    public async Task<ActionResult<CompanyResponse>> Update(Guid id, UpdateCompanyRequest request, CancellationToken ct)
    {
        var result = await companies.UpdateAsync(id, request.Name, request.Code, request.IsActive, request.EditorVersion, ct);
        return result.Status switch
        {
            CompanyMutationStatus.Succeeded => Ok(Map(result.Company!)), CompanyMutationStatus.NotFound => NotFound(),
            CompanyMutationStatus.DuplicateCode => Conflict(new ProblemDetails { Title = "Company code already exists." }),
            CompanyMutationStatus.Forbidden => Forbid(), _ => Conflict(new ProblemDetails { Title = "Company was changed. Reload and try again." })
        };
    }

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Company.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await companies.DeleteAsync(id, ct);
        return result.Status switch { CompanyMutationStatus.Succeeded => NoContent(), CompanyMutationStatus.NotFound => NotFound(), CompanyMutationStatus.Forbidden => Forbid(), CompanyMutationStatus.InUse => Conflict(new ProblemDetails { Title = "Company has users or clients and cannot be deleted. Deactivate it instead." }), _ => Conflict() };
    }

    private static CompanyResponse Map(CompanySummary x) => new(x.Id, x.Name, x.Code, x.IsActive, x.UserCount, x.ClientCount, x.EditorVersion);
}
