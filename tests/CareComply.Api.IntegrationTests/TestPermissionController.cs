using CareComply.Api.Authorization;
using CareComply.Application.Authorization;
using CareComply.Application.Behaviors;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace CareComply.Api.IntegrationTests;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/test/permission")]
public sealed class TestPermissionController(
    AuthorizationBehavior<TestPermissionCommand, TestPermissionResult> authorizationBehavior)
    : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Client.Create)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await authorizationBehavior.Handle(
            new TestPermissionCommand(),
            static (_, _) => ValueTask.FromResult(new TestPermissionResult(true, null)),
            cancellationToken);

        return result switch
        {
            { Handled: true } => NoContent(),
            { Failure: AuthorizationFailure.Unauthenticated } => Unauthorized(),
            { Failure: AuthorizationFailure.Forbidden } => Forbid(),
            _ => throw new InvalidOperationException("The test authorization result is invalid."),
        };
    }
}

public sealed record TestPermissionCommand : ICommand<TestPermissionResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.Client.Create;
}

public sealed record TestPermissionResult(bool Handled, AuthorizationFailure? Failure)
    : IAuthorizationFailureResponse<TestPermissionResult>
{
    public static TestPermissionResult Denied(AuthorizationFailure failure) => new(false, failure);
}
