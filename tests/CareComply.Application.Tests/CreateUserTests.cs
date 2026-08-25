using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Application.Features.AccessManagement.Roles.Queries.ListRoles;
using CareComply.Application.Features.AccessManagement.Users.Commands.CreateUser;

namespace CareComply.Application.Tests;

public sealed class CreateUserTests
{
    [Fact]
    public async Task CreateRequiresManageNormalizesInputForwardsCancellationAndAuditsSafely()
    {
        var firstRole = Guid.CreateVersion7();
        var secondRole = Guid.CreateVersion7();
        using var source = new CancellationTokenSource();
        var provisioning = new CapturingProvisioning();
        var command = new CreateUserCommand(
            " Ada ",
            " Lovelace ",
            " ada@example.test ",
            " +1 555 0100 ",
            true,
            true,
            UserMfaMethod.Email,
            [firstRole, secondRole]);

        var result = await new CreateUserCommandHandler(provisioning)
            .Handle(command, source.Token);
        var audit = command.CreateAuditEvent(result);

        Assert.Equal(Permissions.User.Manage, command.RequiredPermission);
        Assert.Equal("Ada", provisioning.Request!.FirstName);
        Assert.Equal("Lovelace", provisioning.Request.LastName);
        Assert.Equal("ada@example.test", provisioning.Request.Email);
        Assert.Equal("+1 555 0100", provisioning.Request.PhoneNumber);
        Assert.True(provisioning.Request.RequiresMfa);
        Assert.Equal(UserMfaMethod.Email, provisioning.Request.MfaMethod);
        Assert.Equal([firstRole, secondRole], provisioning.Request.RoleIds);
        Assert.Equal(source.Token, provisioning.CancellationToken);
        Assert.Equal("User.Created", audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(provisioning.UserId.ToString("N"), audit.ResourceId);
        Assert.Equal(["isActive", "mfaMethod", "requiresMfa", "roleCount"], audit.Metadata.Entries.Select(entry => entry.Key));
        Assert.DoesNotContain("Ada", audit.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("@example", string.Join('|', audit.Metadata.Entries.Select(entry => entry.Value)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidatorRejectsMissingInvalidDuplicateOrExcessiveRolesAndUnsafeFields()
    {
        var duplicateRole = Guid.CreateVersion7();
        var invalid = new CreateUserCommand(
            " ",
            "",
            "not-email",
            "javascript:alert(1)",
            true,
            false,
            UserMfaMethod.Authenticator,
            [duplicateRole, duplicateRole]);
        var excessive = new CreateUserCommand(
            "Valid",
            "User",
            "valid@example.test",
            null,
            true,
            false,
            UserMfaMethod.Authenticator,
            Enumerable.Range(0, 11).Select(_ => Guid.CreateVersion7()).ToArray());
        var missingRoles = new CreateUserCommand(
            "Valid", "User", "valid@example.test", null, true, false,
            UserMfaMethod.Authenticator, []);

        Assert.False((await new CreateUserCommandValidator().ValidateAsync(invalid)).IsValid);
        Assert.False((await new CreateUserCommandValidator().ValidateAsync(excessive)).IsValid);
        Assert.False((await new CreateUserCommandValidator().ValidateAsync(missingRoles)).IsValid);
    }

    [Fact]
    public async Task RoleMetadataRequiresManageAndForwardsCancellation()
    {
        using var source = new CancellationTokenSource();
        var directory = new CapturingRoleDirectory();
        var query = new ListRolesQuery();

        var result = await new ListRolesQueryHandler(directory).Handle(query, source.Token);

        Assert.Equal(Permissions.User.Manage, query.RequiredPermission);
        Assert.Equal(source.Token, directory.CancellationToken);
        var role = Assert.Single(result.Roles!);
        Assert.Equal([Permissions.User.View, Permissions.User.Manage], role.Permissions);
        Assert.True(role.RequiresMfa);
    }

    [Fact]
    public void ExpectedIdentityFailureCreatesOnlyStableFailedAuditMetadata()
    {
        var command = new CreateUserCommand(
            "Sensitive",
            "Person",
            "sensitive@example.test",
            "+1 555 0199",
            true,
            false,
            UserMfaMethod.Authenticator,
            [Guid.CreateVersion7()]);
        var result = CreateUserResult.Completed(new(
            UserProvisioningStatus.DuplicateEmail,
            null));

        var audit = command.CreateAuditEvent(result);

        Assert.Equal(AuditOutcome.Failed, audit.Outcome);
        var metadata = Assert.Single(audit.Metadata.Entries);
        Assert.Equal("reasonCode", metadata.Key);
        Assert.Equal("duplicate-email", metadata.Value);
        Assert.DoesNotContain("sensitive", metadata.Value, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CapturingProvisioning : IUserProvisioning
    {
        public Guid UserId { get; } = Guid.CreateVersion7();
        public UserProvisioningRequest? Request { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<UserProvisioningResult> CreateAsync(
            UserProvisioningRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            CancellationToken = cancellationToken;
            return Task.FromResult(new UserProvisioningResult(
                UserProvisioningStatus.Succeeded,
                UserId));
        }
    }

    private sealed class CapturingRoleDirectory : IRoleDirectory
    {
        public CancellationToken CancellationToken { get; private set; }

        public Task<IReadOnlyList<RoleDirectoryItem>> ListAsync(
            CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return Task.FromResult<IReadOnlyList<RoleDirectoryItem>>([
                new(
                    Guid.CreateVersion7(),
                    "Database role",
                    [Permissions.User.View, Permissions.User.Manage],
                    true),
            ]);
        }
    }
}
