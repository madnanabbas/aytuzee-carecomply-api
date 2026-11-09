using CareComply.Application.Abstractions.Tenancy;
using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Authorization;
using CareComply.Application.Behaviors;
using CareComply.Application.Features.FormTemplates;
using CareComply.Domain.Forms;

namespace CareComply.Application.Tests;

public sealed class FormTemplateAdministrationTests
{
    [Fact]
    public void CommandsUseCanonicalPermissionsAndPlatformMarker()
    {
        using var stream = new MemoryStream("%PDF-test"u8.ToArray());
        var upload = new UploadFormTemplateCommand(
            Guid.NewGuid(), Guid.NewGuid(), "test", "Test", null, "DHS", "test.pdf",
            null, null, stream);
        var version = new UploadFormTemplateVersionCommand(
            Guid.NewGuid(), Guid.NewGuid(), "test.pdf", null, null, stream);
        var retire = new RetireFormTemplateVersionCommand(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(Permissions.FormTemplate.Manage, upload.RequiredPermission);
        Assert.Equal(Permissions.FormTemplate.Manage, version.RequiredPermission);
        Assert.Equal(Permissions.FormTemplate.Manage, retire.RequiredPermission);
        Assert.IsAssignableFrom<IRequirePlatformAdministrator>(upload);
        Assert.IsAssignableFrom<IRequirePlatformAdministrator>(version);
        Assert.IsAssignableFrom<IRequirePlatformAdministrator>(retire);
        Assert.Equal(Permissions.FormTemplate.View, new ListFormTemplatesQuery().RequiredPermission);
    }

    [Fact]
    public async Task PlatformBehaviorFailsClosedForCompanyAdministrator()
    {
        var behavior = new PlatformAdministratorAuthorizationBehavior<
            RetireFormTemplateVersionCommand, FormTemplateOperationResult>(
            new StubTenantScopeAccessor(new TenantScope(
                Guid.NewGuid(), Guid.NewGuid(), "Synthetic Company", false)));
        var called = false;

        var result = await behavior.Handle(
            new RetireFormTemplateVersionCommand(Guid.NewGuid(), Guid.NewGuid()),
            (_, _) =>
            {
                called = true;
                return ValueTask.FromResult(FormTemplateOperationResult.Invalid(
                    FormTemplateFileValidationError.None));
            },
            CancellationToken.None);

        Assert.Equal(AuthorizationFailure.Forbidden, result.AuthorizationFailure);
        Assert.False(called);
    }

    [Fact]
    public async Task PlatformBehaviorAllowsPlatformAdministrator()
    {
        var behavior = new PlatformAdministratorAuthorizationBehavior<
            RetireFormTemplateVersionCommand, FormTemplateOperationResult>(
            new StubTenantScopeAccessor(new TenantScope(
                Guid.NewGuid(), null, null, true)));
        var called = false;

        _ = await behavior.Handle(
            new RetireFormTemplateVersionCommand(Guid.NewGuid(), Guid.NewGuid()),
            (_, _) =>
            {
                called = true;
                return ValueTask.FromResult(FormTemplateOperationResult.Invalid(
                    FormTemplateFileValidationError.None));
            },
            CancellationToken.None);

        Assert.True(called);
    }

    private sealed class StubTenantScopeAccessor(TenantScope? scope) : ITenantScopeAccessor
    {
        public Task<TenantScope?> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(scope);
    }
}
