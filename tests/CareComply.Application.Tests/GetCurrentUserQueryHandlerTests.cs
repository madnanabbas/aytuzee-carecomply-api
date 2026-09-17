using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.Authorization;
using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Features.Authentication.Queries.GetCurrentUser;

namespace CareComply.Application.Tests;

public sealed class GetCurrentUserQueryHandlerTests
{
    [Fact]
    public async Task UnauthenticatedActorFailsClosedWithoutIdentityLookup()
    {
        var reader = new StubIdentityUserReader(null);
        var handler = new GetCurrentUserQueryHandler(
            new StubCurrentActor(false, null),
            reader,
            new StubUserPermissionResolver(new HashSet<string>(StringComparer.Ordinal)));

        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        Assert.Null(result);
        Assert.False(reader.WasCalled);
    }

    [Fact]
    public async Task AuthenticatedActorReturnsCurrentUser()
    {
        var userId = Guid.CreateVersion7();
        var companyId = Guid.CreateVersion7();
        var reader = new StubIdentityUserReader(
            new IdentityUserSnapshot(
                userId,
                "person@example.test",
                "Test Person",
                companyId,
                "Contoso Care",
                false));
        var handler = new GetCurrentUserQueryHandler(
            new StubCurrentActor(true, userId),
            reader,
            new StubUserPermissionResolver(
                new HashSet<string>(["Client.View"], StringComparer.Ordinal)));

        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(userId, result.Id);
        Assert.Equal("person@example.test", result.Email);
        Assert.Equal("Test Person", result.DisplayName);
        Assert.Equal(companyId, result.CompanyId);
        Assert.Equal("Contoso Care", result.CompanyName);
        Assert.False(result.IsPlatformAdministrator);
        Assert.Equal(["Client.View"], result.Permissions);
    }

    [Fact]
    public async Task AuthenticatedActorWhoseUserNoLongerExistsFailsClosed()
    {
        var userId = Guid.CreateVersion7();
        var reader = new StubIdentityUserReader(null);
        var handler = new GetCurrentUserQueryHandler(
            new StubCurrentActor(true, userId),
            reader,
            new StubUserPermissionResolver(new HashSet<string>(StringComparer.Ordinal)));
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await handler.Handle(
            new GetCurrentUserQuery(),
            cancellationTokenSource.Token);

        Assert.Null(result);
        Assert.True(reader.WasCalled);
        Assert.Equal(userId, reader.UserId);
        Assert.Equal(cancellationTokenSource.Token, reader.CancellationToken);
    }

    private sealed record StubCurrentActor(bool IsAuthenticated, Guid? UserId) : ICurrentActor;

    private sealed class StubIdentityUserReader(IdentityUserSnapshot? user) : IIdentityUserReader
    {
        public bool WasCalled { get; private set; }

        public Guid? UserId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<IdentityUserSnapshot?> FindByIdAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            UserId = userId;
            CancellationToken = cancellationToken;
            return Task.FromResult(user);
        }
    }

    private sealed class StubUserPermissionResolver(IReadOnlySet<string> permissions)
        : IUserPermissionResolver
    {
        public Task<IReadOnlySet<string>> ResolveAsync(
            Guid userId,
            CancellationToken cancellationToken) => Task.FromResult(permissions);
    }
}
