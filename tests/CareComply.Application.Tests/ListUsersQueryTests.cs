using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Authorization;
using CareComply.Application.Features.AccessManagement.Users.Queries.ListUsers;

namespace CareComply.Application.Tests;

public sealed class ListUsersQueryTests
{
    [Fact]
    public void QueryRequiresCanonicalUserViewPermission()
    {
        var query = CreateQuery();

        Assert.Equal(Permissions.User.View, query.RequiredPermission);
    }

    [Fact]
    public async Task ValidatorAcceptsBoundariesAndRejectsInvalidPagingAndAllowlistValues()
    {
        var validator = new ListUsersQueryValidator();
        var minimum = await validator.ValidateAsync(CreateQuery(pageNumber: 1, pageSize: 1));
        var maximum = await validator.ValidateAsync(CreateQuery(pageNumber: 1, pageSize: 100));
        var invalid = await validator.ValidateAsync(new ListUsersQuery(
            new string('x', 201),
            null,
            null,
            "required",
            "passwordHash",
            "sideways",
            1_000_001,
            101));

        Assert.True(minimum.IsValid);
        Assert.True(maximum.IsValid);
        Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Errors, error => error.PropertyName == nameof(ListUsersQuery.Search));
        Assert.Contains(invalid.Errors, error => error.PropertyName == nameof(ListUsersQuery.MfaStatus));
        Assert.Contains(invalid.Errors, error => error.PropertyName == nameof(ListUsersQuery.SortBy));
        Assert.Contains(invalid.Errors, error => error.PropertyName == nameof(ListUsersQuery.SortDirection));
        Assert.Contains(invalid.Errors, error => error.PropertyName == nameof(ListUsersQuery.PageNumber));
        Assert.Contains(invalid.Errors, error => error.PropertyName == nameof(ListUsersQuery.PageSize));
    }

    [Fact]
    public async Task HandlerNormalizesAndForwardsRequestAndCancellation()
    {
        var cancellation = new CancellationTokenSource();
        var page = new UserDirectoryPage(
            [new UserDirectoryItem(
                Guid.CreateVersion7(),
                "Ada Lovelace",
                "ada@example.test",
                true,
                true,
                [new UserDirectoryRole(Guid.CreateVersion7(), "Administrator")])],
            [],
            2,
            10,
            11);
        var directory = new CapturingUserDirectory(page);
        var handler = new ListUsersQueryHandler(directory);

        var result = await handler.Handle(
            new ListUsersQuery(
                "  Ada  ",
                true,
                Guid.CreateVersion7(),
                "notEnabled",
                "email",
                "desc",
                2,
                10),
            cancellation.Token);

        Assert.Same(page, result.Page);
        Assert.Null(result.AuthorizationFailure);
        Assert.NotNull(directory.Request);
        Assert.Equal("Ada", directory.Request.Search);
        Assert.True(directory.Request.IsActive);
        Assert.Equal(UserDirectoryMfaFilter.NotEnabled, directory.Request.MfaStatus);
        Assert.Equal(UserDirectorySort.Email, directory.Request.Sort);
        Assert.True(directory.Request.SortDescending);
        Assert.Equal(2, directory.Request.PageNumber);
        Assert.Equal(10, directory.Request.PageSize);
        Assert.Equal(cancellation.Token, directory.CancellationToken);
    }

    [Fact]
    public void AuthorizationFailureResponseReturnsNoPage()
    {
        var result = ListUsersResult.Denied(AuthorizationFailure.Forbidden);

        Assert.Null(result.Page);
        Assert.Equal(AuthorizationFailure.Forbidden, result.AuthorizationFailure);
    }

    private static ListUsersQuery CreateQuery(int pageNumber = 1, int pageSize = 25) => new(
        null,
        null,
        null,
        null,
        "name",
        "asc",
        pageNumber,
        pageSize);

    private sealed class CapturingUserDirectory(UserDirectoryPage page) : IUserDirectory
    {
        public UserDirectoryRequest? Request { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<UserDirectoryPage> ListAsync(
            UserDirectoryRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            CancellationToken = cancellationToken;
            return Task.FromResult(page);
        }

        public Task<UserDirectoryDetails?> GetByIdAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
