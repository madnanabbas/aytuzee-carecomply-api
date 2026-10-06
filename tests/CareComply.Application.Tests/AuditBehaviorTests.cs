using CareComply.Application.Abstractions.Auditing;
using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.Authorization;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Application.Behaviors;
using CareComply.Application.Exceptions;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Tests;

public sealed class AuditBehaviorTests
{
    [Fact]
    public async Task SuccessfulRequestCapturesActorTraceAndResponseDerivedEvent()
    {
        var actorId = Guid.CreateVersion7();
        var resourceId = Guid.CreateVersion7().ToString("N");
        var execution = new RecordingAuditExecution();
        var behavior = new AuditBehavior<AuditableCommand, AuditableResult>(
            new StubCurrentActor(true, actorId),
            new StubTraceContext("0123456789abcdef0123456789abcdef"),
            execution);
        var handlerCompleted = false;

        var result = await behavior.Handle(
            new AuditableCommand("valid"),
            (_, _) =>
            {
                handlerCompleted = true;
                return ValueTask.FromResult(new AuditableResult(resourceId, AuditOutcome.Succeeded));
            },
            CancellationToken.None);

        Assert.True(handlerCompleted);
        Assert.Equal(resourceId, result.ResourceId);
        Assert.Equal(actorId, execution.ActorUserId);
        Assert.Equal("0123456789abcdef0123456789abcdef", execution.TraceId);
        Assert.NotNull(execution.Event);
        Assert.Equal(resourceId, execution.Event.ResourceId);
        Assert.Equal(AuditOutcome.Succeeded, execution.Event.Outcome);
        Assert.Equal(1, execution.CallCount);
    }

    [Fact]
    public async Task MissingActorFailsBeforeExecutionOrHandler()
    {
        var execution = new RecordingAuditExecution();
        var behavior = new AuditBehavior<AuditableCommand, AuditableResult>(
            new StubCurrentActor(false, null),
            new StubTraceContext(null),
            execution);
        var handlerCalled = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new AuditableCommand("valid"),
            (_, _) =>
            {
                handlerCalled = true;
                return ValueTask.FromResult(
                    new AuditableResult("resource", AuditOutcome.Succeeded));
            },
            CancellationToken.None).AsTask());

        Assert.False(handlerCalled);
        Assert.Equal(0, execution.CallCount);
    }

    [Fact]
    public async Task CancellationAndUnexpectedExceptionsPropagateWithoutEvent()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var execution = new RecordingAuditExecution();
        var behavior = new AuditBehavior<AuditableCommand, AuditableResult>(
            new StubCurrentActor(true, Guid.CreateVersion7()),
            new StubTraceContext(null),
            execution);

        await Assert.ThrowsAsync<OperationCanceledException>(() => behavior.Handle(
            new AuditableCommand("valid"),
            (_, token) =>
            {
                token.ThrowIfCancellationRequested();
                return ValueTask.FromResult(
                    new AuditableResult("resource", AuditOutcome.Succeeded));
            },
            cancellationSource.Token).AsTask());

        Assert.Null(execution.Event);
    }

    [Fact]
    public async Task PipelineRunsValidationThenAuthorizationThenAuditThenHandler()
    {
        var resolver = new StubPermissionResolver([Permissions.Client.Create]);
        var validation = new ValidationBehavior<AuditableCommand, AuditableResult>(
            [new AuditableCommandValidator()]);
        var authorization = new AuthorizationBehavior<AuditableCommand, AuditableResult>(
            new StubCurrentActor(true, Guid.CreateVersion7()),
            resolver);
        var execution = new RecordingAuditExecution();
        var audit = new AuditBehavior<AuditableCommand, AuditableResult>(
            new StubCurrentActor(true, Guid.CreateVersion7()),
            new StubTraceContext(null),
            execution);
        var handlerCallCount = 0;

        await Assert.ThrowsAsync<ApplicationValidationException>(() => validation.Handle(
            new AuditableCommand(string.Empty),
            Authorize,
            CancellationToken.None).AsTask());
        Assert.Equal(0, resolver.CallCount);
        Assert.Equal(0, execution.CallCount);
        Assert.Equal(0, handlerCallCount);

        resolver.Permissions = new HashSet<string>(StringComparer.Ordinal);
        var denied = await validation.Handle(
            new AuditableCommand("valid"),
            Authorize,
            CancellationToken.None);
        Assert.Equal(AuditOutcome.Failed, denied.Outcome);
        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(0, execution.CallCount);
        Assert.Equal(0, handlerCallCount);

        resolver.Permissions = new HashSet<string>([Permissions.Client.Create], StringComparer.Ordinal);
        var allowed = await validation.Handle(
            new AuditableCommand("valid"),
            Authorize,
            CancellationToken.None);
        Assert.Equal(AuditOutcome.Succeeded, allowed.Outcome);
        Assert.Equal(1, execution.CallCount);
        Assert.Equal(1, handlerCallCount);
        return;

        ValueTask<AuditableResult> Authorize(
            AuditableCommand message,
            CancellationToken token) => authorization.Handle(message, Audit, token);

        ValueTask<AuditableResult> Audit(
            AuditableCommand message,
            CancellationToken token) => audit.Handle(message, Handle, token);

        ValueTask<AuditableResult> Handle(
            AuditableCommand message,
            CancellationToken token)
        {
            handlerCallCount++;
            return ValueTask.FromResult(
                new AuditableResult(Guid.CreateVersion7().ToString("N"), AuditOutcome.Succeeded));
        }
    }

    [Fact]
    public void BehaviorIsConstrainedToExplicitAuditableRequestsAndUsesNoServiceLocator()
    {
        var genericParameter = typeof(AuditBehavior<,>).GetGenericArguments()[0];
        Assert.Contains(
            genericParameter.GetGenericParameterConstraints(),
            constraint => constraint.IsGenericType
                && constraint.GetGenericTypeDefinition() == typeof(IAuditableRequest<>));
        var constructor = Assert.Single(
            typeof(AuditBehavior<AuditableCommand, AuditableResult>).GetConstructors());
        Assert.DoesNotContain(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(IServiceProvider));
    }

    private sealed record AuditableCommand(string Value)
        : ICommand<AuditableResult>, IRequirePermission, IAuditableRequest<AuditableResult>
    {
        public string RequiredPermission => Permissions.Client.Create;

        public AuditEvent CreateAuditEvent(AuditableResult response) => new(
            "AuditFoundation.Executed",
            "AuditFoundation",
            response.ResourceId,
            response.Outcome);
    }

    private sealed record AuditableResult(string ResourceId, AuditOutcome Outcome)
        : IAuthorizationFailureResponse<AuditableResult>
    {
        public static AuditableResult Denied(AuthorizationFailure failure) =>
            new("authorization-denied", AuditOutcome.Failed);
    }

    private sealed class AuditableCommandValidator : AbstractValidator<AuditableCommand>
    {
        public AuditableCommandValidator() => RuleFor(command => command.Value).NotEmpty();
    }

    private sealed record StubCurrentActor(bool IsAuthenticated, Guid? UserId) : ICurrentActor;

    private sealed record StubTraceContext(string? TraceId) : IAuditTraceContext;

    private sealed class StubPermissionResolver(IEnumerable<string> permissions)
        : IUserPermissionResolver
    {
        public IReadOnlySet<string> Permissions { get; set; } =
            new HashSet<string>(permissions, StringComparer.Ordinal);

        public int CallCount { get; private set; }

        public Task<IReadOnlySet<string>> ResolveAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(Permissions);
        }
    }

    private sealed class RecordingAuditExecution : IAuditExecution
    {
        public Guid ActorUserId { get; private set; }

        public string? TraceId { get; private set; }

        public AuditEvent? Event { get; private set; }

        public int CallCount { get; private set; }

        public async ValueTask<TResponse> ExecuteAsync<TResponse>(
            Guid actorUserId,
            string? traceId,
            Func<CancellationToken, ValueTask<TResponse>> operation,
            Func<TResponse, AuditEvent> eventFactory,
            CancellationToken cancellationToken)
        {
            CallCount++;
            ActorUserId = actorUserId;
            TraceId = traceId;
            var response = await operation(cancellationToken);
            Event = eventFactory(response);
            return response;
        }
    }
}
