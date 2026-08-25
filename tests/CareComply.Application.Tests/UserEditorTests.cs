using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Application.Features.AccessManagement.Users.Commands.SetUserStatus;
using CareComply.Application.Features.AccessManagement.Users.Commands.UpdateUserGeneral;
using CareComply.Application.Features.AccessManagement.Users.Queries.GetUser;
using CareComply.Application.Features.AccessManagement.Users.Commands.SetUserMfa;
using CareComply.Application.Features.AccessManagement.Users.Commands.ReplaceUserRoles;

namespace CareComply.Application.Tests;

public sealed class UserEditorTests
{
    [Fact]
    public async Task GetUserRequiresViewAndForwardsCancellation()
    {
        var userId = Guid.CreateVersion7();
        var directory = new CapturingDirectory();
        using var source = new CancellationTokenSource();
        var query = new GetUserQuery(userId);

        var result = await new GetUserQueryHandler(directory).Handle(query, source.Token);

        Assert.Equal(Permissions.User.View, query.RequiredPermission);
        Assert.Equal(userId, directory.UserId);
        Assert.Equal(source.Token, directory.CancellationToken);
        Assert.Null(result.AuthorizationFailure);
    }

    [Fact]
    public async Task GeneralUpdateRequiresManageNormalizesInputAndCreatesSafeAuditMetadata()
    {
        var administration = new CapturingGeneralAdministration();
        var command = new UpdateUserGeneralCommand(
            Guid.CreateVersion7(), " Ada ", " Lovelace ", " ada@example.test ", " +1 555 0100 ",
            new string('A', 64));

        var result = await new UpdateUserGeneralCommandHandler(administration)
            .Handle(command, CancellationToken.None);
        var audit = command.CreateAuditEvent(result);

        Assert.Equal(Permissions.User.Manage, command.RequiredPermission);
        Assert.Equal("Ada", administration.Request!.FirstName);
        Assert.Equal("User.GeneralUpdated", audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal("changedFields", Assert.Single(audit.Metadata.Entries).Key);
        Assert.DoesNotContain("@example", audit.Metadata.Entries[0].Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeactivationRejectsCurrentActorWithoutCallingIdentityAdapter()
    {
        var actorId = Guid.CreateVersion7();
        var administration = new CapturingStatusAdministration();
        var handler = new SetUserStatusCommandHandler(administration, new TestActor(actorId));
        var command = new SetUserStatusCommand(actorId, false, new string('A', 64));

        var result = await handler.Handle(command, CancellationToken.None);
        var audit = command.CreateAuditEvent(result);

        Assert.True(result.SelfDeactivationDenied);
        Assert.False(administration.WasCalled);
        Assert.Equal(AuditOutcome.Failed, audit.Outcome);
        Assert.Equal("SelfDeactivationDenied", Assert.Single(audit.Metadata.Entries).Value);
    }

    [Fact]
    public async Task ValidatorsRejectUnsafeOrIncompleteEditorInput()
    {
        var invalidGeneral = new UpdateUserGeneralCommand(
            Guid.Empty, "", "", "not-email", "javascript:alert(1)", "raw-stamp");
        var invalidStatus = new SetUserStatusCommand(Guid.Empty, true, "short");
        var general = await new UpdateUserGeneralCommandValidator().ValidateAsync(invalidGeneral);
        var status = await new SetUserStatusCommandValidator().ValidateAsync(invalidStatus);

        Assert.False(general.IsValid);
        Assert.False(status.IsValid);
    }

    [Fact]
    public async Task MfaUpdateRequiresManageForwardsCancellationAndAuditsOnlyStableState()
    {
        var userId = Guid.CreateVersion7();
        using var source = new CancellationTokenSource();
        var administration = new CapturingMfaAdministration();
        var command = new SetUserMfaCommand(
            userId,
            true,
            UserMfaMethod.Email,
            new string('A', 64));

        var result = await new SetUserMfaCommandHandler(administration)
            .Handle(command, source.Token);
        var audit = command.CreateAuditEvent(result);

        Assert.Equal(Permissions.User.Manage, command.RequiredPermission);
        Assert.Equal(userId, administration.UserId);
        Assert.True(administration.IsEnabled);
        Assert.Equal(source.Token, administration.CancellationToken);
        Assert.Equal("User.MfaEnabled", audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal("Email", Assert.Single(audit.Metadata.Entries).Value);
    }

    [Fact]
    public async Task RoleReplacementRequiresManageForwardsIdsAndAuditsOnlyChangedRoleIds()
    {
        var userId = Guid.CreateVersion7();
        var addedRoleId = Guid.CreateVersion7();
        var removedRoleId = Guid.CreateVersion7();
        using var source = new CancellationTokenSource();
        var administration = new CapturingRoleAdministration(addedRoleId, removedRoleId);
        var command = new ReplaceUserRolesCommand(
            userId,
            [addedRoleId],
            new string('A', 64));

        var result = await new ReplaceUserRolesCommandHandler(administration)
            .Handle(command, source.Token);
        var audit = command.CreateAuditEvent(result);

        Assert.Equal(Permissions.User.Manage, command.RequiredPermission);
        Assert.Equal(userId, administration.UserId);
        Assert.Equal([addedRoleId], administration.RoleIds);
        Assert.Equal(source.Token, administration.CancellationToken);
        Assert.Equal("User.RolesChanged", audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(["addedRoleIds1", "removedRoleIds1"],
            audit.Metadata.Entries.Select(entry => entry.Key));
        var values = string.Join(',', audit.Metadata.Entries.Select(entry => entry.Value));
        Assert.Contains(addedRoleId.ToString("N"), values, StringComparison.Ordinal);
        Assert.Contains(removedRoleId.ToString("N"), values, StringComparison.Ordinal);
        Assert.DoesNotContain("role-name", values, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RoleReplacementValidatorRejectsMissingDuplicateAndExcessiveRoleIds()
    {
        var duplicate = Guid.CreateVersion7();
        var validator = new ReplaceUserRolesCommandValidator();
        var missing = new ReplaceUserRolesCommand(Guid.Empty, [], "short");
        var duplicated = new ReplaceUserRolesCommand(
            Guid.CreateVersion7(), [duplicate, duplicate], new string('A', 64));
        var excessive = new ReplaceUserRolesCommand(
            Guid.CreateVersion7(),
            Enumerable.Range(0, 11).Select(_ => Guid.CreateVersion7()).ToArray(),
            new string('A', 64));

        Assert.False((await validator.ValidateAsync(missing)).IsValid);
        Assert.False((await validator.ValidateAsync(duplicated)).IsValid);
        Assert.False((await validator.ValidateAsync(excessive)).IsValid);
    }

    private sealed class CapturingDirectory : IUserDirectory
    {
        public Guid UserId { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public Task<UserDirectoryDetails?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            UserId = userId;
            CancellationToken = cancellationToken;
            return Task.FromResult<UserDirectoryDetails?>(null);
        }
        public Task<UserDirectoryPage> ListAsync(UserDirectoryRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingGeneralAdministration : IUserGeneralAdministration
    {
        public UserGeneralUpdateRequest? Request { get; private set; }
        public Task<UserGeneralUpdateResult> UpdateAsync(UserGeneralUpdateRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new UserGeneralUpdateResult(
                UserAdministrationStatus.Succeeded,
                null,
                ["FirstName", "Email"]));
        }
    }

    private sealed class CapturingStatusAdministration : IUserStatusAdministration
    {
        public bool WasCalled { get; private set; }
        public Task<UserStatusUpdateResult> SetActiveAsync(Guid userId, bool isActive, string editorVersion, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(new UserStatusUpdateResult(UserAdministrationStatus.Succeeded, null));
        }
    }

    private sealed class TestActor(Guid userId) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
    }

    private sealed class CapturingMfaAdministration : IUserMfaAdministration
    {
        public Guid UserId { get; private set; }
        public bool IsEnabled { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<UserMfaUpdateResult> SetAsync(
            Guid userId,
            bool isEnabled,
            UserMfaMethod method,
            string editorVersion,
            CancellationToken cancellationToken)
        {
            UserId = userId;
            IsEnabled = isEnabled;
            CancellationToken = cancellationToken;
            return Task.FromResult(new UserMfaUpdateResult(UserMfaUpdateStatus.Succeeded, null));
        }
    }

    private sealed class CapturingRoleAdministration(
        Guid addedRoleId,
        Guid removedRoleId) : IUserRoleAdministration
    {
        public Guid UserId { get; private set; }
        public IReadOnlyList<Guid> RoleIds { get; private set; } = [];
        public CancellationToken CancellationToken { get; private set; }

        public Task<UserRoleUpdateResult> ReplaceAsync(
            Guid userId,
            IReadOnlyList<Guid> roleIds,
            string editorVersion,
            CancellationToken cancellationToken)
        {
            UserId = userId;
            RoleIds = roleIds;
            CancellationToken = cancellationToken;
            return Task.FromResult(new UserRoleUpdateResult(
                UserRoleUpdateStatus.Succeeded,
                null,
                [addedRoleId],
                [removedRoleId]));
        }
    }
}
