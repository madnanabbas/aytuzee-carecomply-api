using CareComply.Api.Authorization;
using CareComply.Api.Errors;
using CareComply.Application.Abstractions.Employees;
using CareComply.Application.Authorization;
using CareComply.Application.Features.Employees.CreateEmployee;
using CareComply.Application.Features.Employees.UpdateEmployee;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.Controllers;

[ApiController]
[Route("api/employees")]
public sealed class EmployeesController(
    ISender sender,
    IEmployeeDirectory employeeDirectory) : ControllerBase
{
    private const int MaximumPageSize = 100;

    [HttpGet]
    [HasPermission(Permissions.Employee.View)]
    public async Task<ActionResult<EmployeeListResponse>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            ModelState.AddModelError(nameof(page), "Page must be at least 1.");
        }
        if (pageSize is < 1 or > MaximumPageSize)
        {
            ModelState.AddModelError(nameof(pageSize), $"Page size must be between 1 and {MaximumPageSize}.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await employeeDirectory.ListAsync(
            new(string.IsNullOrWhiteSpace(search) ? null : search.Trim(), page, pageSize),
            cancellationToken);
        if (result is null)
        {
            return Forbid();
        }

        var totalPages = result.TotalCount == 0
            ? 0
            : (int)Math.Ceiling(result.TotalCount / (double)result.PageSize);
        return Ok(new EmployeeListResponse(
            result.Items.Select(MapListItem).ToArray(),
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            totalPages));
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Employee.View)]
    public async Task<ActionResult<EmployeeDetailsResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var employee = await employeeDirectory.GetAsync(id, cancellationToken);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpPost]
    [HasPermission(Permissions.Employee.Create)]
    public async Task<ActionResult<EmployeeDetailsResponse>> Create(
        SaveEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateEmployeeCommand(request),
            cancellationToken);
        if (result.AuthorizationFailure is { } authorizationFailure)
        {
            return MapAuthorizationFailure(authorizationFailure);
        }

        var administration = result.Administration;
        return administration?.Status switch
        {
            EmployeeAdministrationStatus.Succeeded when administration.Employee is { } employee =>
                CreatedAtAction(nameof(Get), new { id = employee.Id }, employee),
            EmployeeAdministrationStatus.Unauthorized => Unauthorized(),
            EmployeeAdministrationStatus.ValidationFailed => ValidationFailure(administration),
            _ => throw new InvalidOperationException("The employee create result is invalid."),
        };
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Employee.Edit)]
    public async Task<ActionResult<EmployeeDetailsResponse>> Update(
        Guid id,
        SaveEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateEmployeeCommand(id, request),
            cancellationToken);
        if (result.AuthorizationFailure is { } authorizationFailure)
        {
            return MapAuthorizationFailure(authorizationFailure);
        }

        var administration = result.Administration;
        return administration?.Status switch
        {
            EmployeeAdministrationStatus.Succeeded when administration.Employee is { } employee =>
                Ok(employee),
            EmployeeAdministrationStatus.NotFound => NotFound(),
            EmployeeAdministrationStatus.Conflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The employee has changed since it was loaded. Refresh the record and try again.",
                type: ProblemTypes.EmployeeConflict),
            EmployeeAdministrationStatus.ValidationFailed => ValidationFailure(administration),
            _ => throw new InvalidOperationException("The employee update result is invalid."),
        };
    }

    private ActionResult<EmployeeDetailsResponse> ValidationFailure(EmployeeAdministrationResult result)
    {
        foreach (var (key, messages) in result.ValidationErrors
            ?? new Dictionary<string, string[]>(StringComparer.Ordinal))
        {
            foreach (var message in messages)
            {
                ModelState.AddModelError(key, message);
            }
        }

        return ValidationProblem(ModelState);
    }

    private ActionResult MapAuthorizationFailure(AuthorizationFailure failure) =>
        failure == AuthorizationFailure.Unauthenticated
            ? Unauthorized()
            : Forbid();

    private static EmployeeListItemResponse MapListItem(EmployeeDirectoryEntry employee) => new(
        employee.Id,
        employee.CompanyId,
        employee.FirstName,
        employee.LastName,
        employee.PreferredName,
        employee.Email,
        employee.Phone,
        employee.JobTitle,
        employee.IsActive,
        employee.HireDate,
        employee.Locations);
}
