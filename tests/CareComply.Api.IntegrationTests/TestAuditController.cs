using CareComply.Api.Authorization;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Application.Behaviors;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Api.IntegrationTests;

[ApiController]
[Route("api/test/audit")]
public sealed class TestAuditController(
    ValidationBehavior<TestAuditCommand, TestAuditResult> validationBehavior,
    AuthorizationBehavior<TestAuditCommand, TestAuditResult> authorizationBehavior,
    AuditBehavior<TestAuditCommand, TestAuditResult> auditBehavior,
    CareComplyDbContext context) : ControllerBase
{
    [HasPermission(Permissions.Client.Create)]
    [HttpPost]
    public async Task<IActionResult> Execute(
        TestAuditRequest request,
        CancellationToken cancellationToken)
    {
        var command = new TestAuditCommand(
            request.Mode ?? string.Empty,
            request.ResourceId,
            request.UnapprovedPayload);
        var result = await validationBehavior.Handle(command, Authorize, cancellationToken);

        return result.AuthorizationFailure switch
        {
            AuthorizationFailure.Unauthenticated => Unauthorized(),
            AuthorizationFailure.Forbidden => Forbid(),
            null => Ok(result),
            _ => throw new InvalidOperationException(
                "The test audit authorization result is invalid."),
        };

        ValueTask<TestAuditResult> Authorize(
            TestAuditCommand message,
            CancellationToken token) => authorizationBehavior.Handle(message, Audit, token);

        ValueTask<TestAuditResult> Audit(
            TestAuditCommand message,
            CancellationToken token) => auditBehavior.Handle(message, Handle, token);

        async ValueTask<TestAuditResult> Handle(
            TestAuditCommand message,
            CancellationToken token)
        {
            if (message.Mode == TestAuditModes.FailedClean)
            {
                return new TestAuditResult(
                    message.ResourceId.ToString("N"),
                    AuditOutcome.Failed,
                    null);
            }

            if (message.Mode == TestAuditModes.Read)
            {
                return new TestAuditResult(
                    message.ResourceId.ToString("N"),
                    AuditOutcome.Succeeded,
                    null);
            }

            var roleName = $"AuditTest-{message.ResourceId:N}";
            context.Roles.Add(new ApplicationRole
            {
                Id = message.ResourceId,
                CompanyId = SqlServerApiFactory.DefaultCompanyId,
                Name = roleName,
                NormalizedName = roleName.ToUpperInvariant(),
            });
            await context.SaveChangesAsync(token);

            if (message.Mode == TestAuditModes.UnexpectedException)
            {
                throw new InvalidOperationException(TestAuditModes.InternalExceptionMessage);
            }

            var outcome = message.Mode == TestAuditModes.FailedMutated
                ? AuditOutcome.Failed
                : AuditOutcome.Succeeded;
            return new TestAuditResult(message.ResourceId.ToString("N"), outcome, null);
        }
    }

    [HasPermission(Permissions.Client.Create)]
    [HttpPost("non-auditable")]
    public IActionResult NonAuditable() => NoContent();
}

public sealed class TestAuditRequest
{
    public string? Mode { get; init; }

    public Guid ResourceId { get; init; }

    public string? UnapprovedPayload { get; init; }
}

public sealed record TestAuditResult(
    string ResourceId,
    AuditOutcome Outcome,
    AuthorizationFailure? AuthorizationFailure);

public sealed class TestAuditCommand(
    string mode,
    Guid resourceId,
    string? unapprovedPayload)
    : ICommand<TestAuditResult>,
        IRequirePermission,
        IAuditableRequest<TestAuditResult>
{
    public string Mode { get; } = mode;

    public Guid ResourceId { get; } = resourceId;

    public string? UnapprovedPayload { get; } = unapprovedPayload;

    public string RequiredPermission => Permissions.Client.Create;

    public AuditEvent CreateAuditEvent(TestAuditResult response)
    {
        var metadata = Mode == TestAuditModes.AuditPersistenceFailure
            ? AuditMetadata.Create(Enumerable.Range(0, AuditMetadata.MaximumEntryCount)
                .Select(index => ($"safeField{index}", new string('x', AuditMetadata.MaximumValueLength)))
                .ToArray())
            : AuditMetadata.Create(("operationMode", Mode));

        return new AuditEvent(
            "AuditFoundation.Executed",
            "AuditFoundation",
            response.ResourceId,
            response.Outcome,
            metadata);
    }
}

public sealed class TestAuditCommandValidator : AbstractValidator<TestAuditCommand>
{
    public TestAuditCommandValidator()
    {
        RuleFor(command => command.Mode).NotEmpty();
        RuleFor(command => command.ResourceId).NotEmpty();
    }
}

public sealed class TestAuditAuthorizationFailureResultFactory
    : IAuthorizationFailureResultFactory<TestAuditResult>
{
    public TestAuditResult Create(AuthorizationFailure failure) => new(
        string.Empty,
        AuditOutcome.Failed,
        failure);
}

public static class TestAuditModes
{
    public const string Success = "success";
    public const string FailedClean = "failed-clean";
    public const string FailedMutated = "failed-mutated";
    public const string Read = "read";
    public const string UnexpectedException = "unexpected-exception";
    public const string AuditPersistenceFailure = "audit-persistence-failure";
    public const string InternalExceptionMessage = "sensitive internal audit test failure";
}
