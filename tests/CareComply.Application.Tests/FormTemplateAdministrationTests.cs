using CareComply.Application.Abstractions.Tenancy;
using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Authorization;
using CareComply.Application.Behaviors;
using CareComply.Application.Features.FormTemplates;
using CareComply.Application.Features.ClientForms;
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
        var mappings = new GetFormTemplateMappingsQuery(Guid.NewGuid(), Guid.NewGuid());
        var catalog = new GetFormTemplateBindingCatalogQuery();

        Assert.Equal(Permissions.FormTemplate.Manage, upload.RequiredPermission);
        Assert.Equal(Permissions.FormTemplate.Manage, version.RequiredPermission);
        Assert.Equal(Permissions.FormTemplate.Manage, retire.RequiredPermission);
        Assert.IsAssignableFrom<IRequirePlatformAdministrator>(upload);
        Assert.IsAssignableFrom<IRequirePlatformAdministrator>(version);
        Assert.IsAssignableFrom<IRequirePlatformAdministrator>(retire);
        Assert.IsAssignableFrom<IRequirePlatformAdministrator>(mappings);
        Assert.IsAssignableFrom<IRequirePlatformAdministrator>(catalog);
        Assert.Equal(Permissions.FormTemplate.Manage, mappings.RequiredPermission);
        Assert.Equal(Permissions.FormTemplate.Manage, catalog.RequiredPermission);
        Assert.Equal(Permissions.FormTemplate.View, new ListFormTemplatesQuery().RequiredPermission);
    }

    [Fact]
    public void BindingCatalogExcludesSsnAndCheckboxBindings()
    {
        Assert.DoesNotContain(ClientFormBindingKeys.Catalog,
            item => item.Key.Contains("ssn", StringComparison.OrdinalIgnoreCase));
        Assert.All(ClientFormBindingKeys.Catalog, item =>
            Assert.DoesNotContain(FormFieldType.CheckBox, item.AllowedPdfFieldTypes));
    }

    [Theory]
    [InlineData("Client Signature", FormFieldPurpose.Signature)]
    [InlineData("I acknowledge this statement", FormFieldPurpose.Attestation)]
    public void SensitiveFieldNamesReceiveAdvisoryPurposeSuggestions(
        string fieldName, FormFieldPurpose expected) =>
        Assert.Equal(expected, FormTemplateMappingPolicy.SuggestPurpose(fieldName));

    [Fact]
    public void MappingPolicyRejectsArbitraryBindingAndBindingOnSignature()
    {
        var data = Field(FormFieldType.Text);
        var signature = Field(FormFieldType.Text, "Client Signature");

        var errors = FormTemplateMappingPolicy.Validate(
            [data, signature],
            [new(data.Id, FormFieldPurpose.Data, "client.ssn"),
             new(signature.Id, FormFieldPurpose.Signature, ClientFormBindingKeys.ClientFirstName)]);

        Assert.Contains("binding-key-not-allowed", errors);
        Assert.Contains("binding-not-allowed-for-purpose", errors);
    }

    [Fact]
    public void DataPurposeWithoutBindingIsRejected()
    {
        var field = Field(FormFieldType.Text);
        Assert.Contains("binding-key-not-allowed",
            FormTemplateMappingPolicy.Validate(
                [field], [new(field.Id, FormFieldPurpose.Data, null)]));
    }

    [Fact]
    public void IncompleteMappingSetIsRejected()
    {
        var field = Field(FormFieldType.Text);
        Assert.Contains("incomplete-mapping-set",
            FormTemplateMappingPolicy.Validate([field], []));
    }

    [Fact]
    public void MappingPolicyRejectsDataBindingForCheckbox()
    {
        var checkbox = Field(FormFieldType.CheckBox);
        var errors = FormTemplateMappingPolicy.Validate(
            [checkbox], [new(checkbox.Id, FormFieldPurpose.Data, ClientFormBindingKeys.ClientFirstName)]);
        Assert.Contains("field-type-not-bindable", errors);
    }

    [Fact]
    public void ManualSignatureAndAttestationAcceptNoBinding()
    {
        var fields = new[] { Field(FormFieldType.Text), Field(FormFieldType.Signature),
            Field(FormFieldType.CheckBox) };
        var errors = FormTemplateMappingPolicy.Validate(fields,
            [new(fields[0].Id, FormFieldPurpose.Manual, null),
             new(fields[1].Id, FormFieldPurpose.Signature, null),
             new(fields[2].Id, FormFieldPurpose.Attestation, null)]);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("Client Signature")]
    [InlineData("Participant acknowledgement")]
    [InlineData("Provider attestation")]
    public void PublishingRejectsSensitiveNamedFieldLeftAsData(string name)
    {
        var field = new FormTemplateFieldDetails(Guid.NewGuid(), name, FormFieldType.Text,
            FormFieldPurpose.Data, ClientFormBindingKeys.ClientFirstName, null, true);
        Assert.Contains("sensitive-purpose-confirmation-required",
            FormTemplateMappingPolicy.FindUnsafePublishedMappings([field]));
    }

    private static FormTemplateFieldDetails Field(FormFieldType type, string name = "Test field") =>
        new(Guid.NewGuid(), name, type, FormFieldPurpose.Manual, null, null, true);

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
