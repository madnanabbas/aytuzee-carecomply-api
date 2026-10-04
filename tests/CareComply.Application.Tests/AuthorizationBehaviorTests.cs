using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.Authorization;
using CareComply.Application.Authorization;
using CareComply.Application.Behaviors;
using CareComply.Application.Exceptions;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Tests;

public sealed class AuthorizationBehaviorTests
{
    [Fact]
    public async Task UnauthenticatedRequestReturnsFailureWithoutResolutionOrHandler()
    {
        var resolver = new StubPermissionResolver(Array.Empty<string>());
        var handlerWasCalled = false;
        var behavior = CreateBehavior(new StubCurrentActor(false, null), resolver);

        var result = await behavior.Handle(
            new ProtectedCommand("valid"),
            Next,
            CancellationToken.None);

        Assert.Equal(AuthorizationFailure.Unauthenticated, result.Failure);
        Assert.False(result.Handled);
        Assert.Equal(0, resolver.CallCount);
        Assert.False(handlerWasCalled);
        return;

        ValueTask<ProtectedResult> Next(
            ProtectedCommand message,
            CancellationToken cancellationToken)
        {
            handlerWasCalled = true;
            return ValueTask.FromResult(new ProtectedResult(true, null));
        }
    }

    [Fact]
    public async Task AuthenticatedRequestWithoutPermissionReturnsForbiddenWithoutHandler()
    {
        var userId = Guid.CreateVersion7();
        var resolver = new StubPermissionResolver(Array.Empty<string>());
        var handlerWasCalled = false;
        var behavior = CreateBehavior(new StubCurrentActor(true, userId), resolver);

        var result = await behavior.Handle(
            new ProtectedCommand("valid"),
            Next,
            CancellationToken.None);

        Assert.Equal(AuthorizationFailure.Forbidden, result.Failure);
        Assert.False(result.Handled);
        Assert.Equal(1, resolver.CallCount);
        Assert.False(handlerWasCalled);
        return;

        ValueTask<ProtectedResult> Next(
            ProtectedCommand message,
            CancellationToken cancellationToken)
        {
            handlerWasCalled = true;
            return ValueTask.FromResult(new ProtectedResult(true, null));
        }
    }

    [Fact]
    public async Task AuthenticatedRequestWithPermissionInvokesHandler()
    {
        var userId = Guid.CreateVersion7();
        var resolver = new StubPermissionResolver([Permissions.Client.Create]);
        var handlerWasCalled = false;
        var behavior = CreateBehavior(new StubCurrentActor(true, userId), resolver);

        var result = await behavior.Handle(
            new ProtectedCommand("valid"),
            Next,
            CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Null(result.Failure);
        Assert.Equal(1, resolver.CallCount);
        Assert.True(handlerWasCalled);
        return;

        ValueTask<ProtectedResult> Next(
            ProtectedCommand message,
            CancellationToken cancellationToken)
        {
            handlerWasCalled = true;
            return ValueTask.FromResult(new ProtectedResult(true, null));
        }
    }

    [Fact]
    public async Task UnknownPermissionFailsClosed()
    {
        var behavior = new AuthorizationBehavior<UnknownPermissionCommand, ProtectedResult>(
            new StubCurrentActor(true, Guid.CreateVersion7()),
            new StubPermissionResolver([Permissions.Client.Create]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new UnknownPermissionCommand(),
            (_, _) => ValueTask.FromResult(new ProtectedResult(true, null)),
            CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task PipelineRunsValidationThenAuthorizationThenHandler()
    {
        var userId = Guid.CreateVersion7();
        var resolver = new StubPermissionResolver([Permissions.Client.Create]);
        var authorization = CreateBehavior(new StubCurrentActor(true, userId), resolver);
        var validation = new ValidationBehavior<ProtectedCommand, ProtectedResult>(
            [new ProtectedCommandValidator()]);
        var handlerCallCount = 0;

        await Assert.ThrowsAsync<ApplicationValidationException>(() => validation.Handle(
            new ProtectedCommand(string.Empty),
            Authorize,
            CancellationToken.None).AsTask());
        Assert.Equal(0, resolver.CallCount);
        Assert.Equal(0, handlerCallCount);

        resolver.Permissions = new HashSet<string>(StringComparer.Ordinal);
        var forbidden = await validation.Handle(
            new ProtectedCommand("valid"),
            Authorize,
            CancellationToken.None);
        Assert.Equal(AuthorizationFailure.Forbidden, forbidden.Failure);
        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(0, handlerCallCount);

        resolver.Permissions = new HashSet<string>(
            [Permissions.Client.Create],
            StringComparer.Ordinal);
        var allowed = await validation.Handle(
            new ProtectedCommand("valid"),
            Authorize,
            CancellationToken.None);
        Assert.True(allowed.Handled);
        Assert.Equal(2, resolver.CallCount);
        Assert.Equal(1, handlerCallCount);
        return;

        ValueTask<ProtectedResult> Authorize(
            ProtectedCommand message,
            CancellationToken cancellationToken) => authorization.Handle(
                message,
                Handle,
                cancellationToken);

        ValueTask<ProtectedResult> Handle(
            ProtectedCommand message,
            CancellationToken cancellationToken)
        {
            handlerCallCount++;
            return ValueTask.FromResult(new ProtectedResult(true, null));
        }
    }

    [Fact]
    public void BehaviorConstructorDoesNotAcceptServiceLocator()
    {
        var constructor = Assert.Single(
            typeof(AuthorizationBehavior<ProtectedCommand, ProtectedResult>).GetConstructors());

        Assert.DoesNotContain(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(IServiceProvider));
    }

    private static AuthorizationBehavior<ProtectedCommand, ProtectedResult> CreateBehavior(
        ICurrentActor currentActor,
        IUserPermissionResolver resolver) => new(
            currentActor,
            resolver);

    private static ValueTask<ProtectedResult> SuccessfulNext(
        ProtectedCommand message,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(new ProtectedResult(true, null));

    private sealed record ProtectedCommand(string Value)
        : ICommand<ProtectedResult>, IRequirePermission
    {
        public string RequiredPermission => Permissions.Client.Create;
    }

    private sealed record UnknownPermissionCommand : ICommand<ProtectedResult>, IRequirePermission
    {
        public string RequiredPermission => "Unknown.Permission";
    }

    private sealed record ProtectedResult(bool Handled, AuthorizationFailure? Failure)
        : IAuthorizationFailureResponse<ProtectedResult>
    {
        public static ProtectedResult Denied(AuthorizationFailure failure) => new(false, failure);
    }

    private sealed class ProtectedCommandValidator : AbstractValidator<ProtectedCommand>
    {
        public ProtectedCommandValidator() =>
            RuleFor(command => command.Value).NotEmpty();
    }

    private sealed record StubCurrentActor(bool IsAuthenticated, Guid? UserId) : ICurrentActor;

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
}
