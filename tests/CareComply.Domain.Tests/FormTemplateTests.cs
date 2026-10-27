using CareComply.Domain.Forms;

namespace CareComply.Domain.Tests;

public sealed class FormTemplateTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PlatformTemplateHasNoCompanyAndNormalizesMetadata()
    {
        var actorId = Guid.NewGuid();

        var template = FormTemplate.Create(
            Guid.NewGuid(),
            " DHS-6893F ",
            " DHS 6893F ",
            " Provider agreement ",
            " DHS ",
            FormTemplateOwnerType.Platform,
            null,
            actorId,
            Now);

        Assert.Equal("DHS-6893F", template.Key);
        Assert.Equal("DHS 6893F", template.Name);
        Assert.Equal("Provider agreement", template.Description);
        Assert.Equal("DHS", template.Category);
        Assert.Equal(FormTemplateOwnerType.Platform, template.OwnerType);
        Assert.Null(template.CompanyId);
        Assert.True(template.IsActive);
        Assert.Equal(actorId, template.CreatedByUserId);
        Assert.Equal(Now, template.CreatedAtUtc);
        Assert.Equal(Now, template.UpdatedAtUtc);
    }

    [Fact]
    public void CompanyTemplateRequiresAndRetainsCompany()
    {
        var companyId = Guid.NewGuid();

        var template = FormTemplate.Create(
            Guid.NewGuid(),
            "future-company-form",
            "Future company form",
            null,
            "Company",
            FormTemplateOwnerType.Company,
            companyId,
            Guid.NewGuid(),
            Now);

        Assert.Equal(FormTemplateOwnerType.Company, template.OwnerType);
        Assert.Equal(companyId, template.CompanyId);
    }

    [Fact]
    public void PlatformTemplateRejectsCompany()
    {
        Assert.Throws<ArgumentException>(() => FormTemplate.Create(
            Guid.NewGuid(), "platform", "Platform", null, "DHS",
            FormTemplateOwnerType.Platform, Guid.NewGuid(), Guid.NewGuid(), Now));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompanyTemplateRejectsMissingOrEmptyCompany(bool useEmptyIdentifier)
    {
        Guid? companyId = useEmptyIdentifier ? Guid.Empty : null;

        Assert.Throws<ArgumentException>(() => FormTemplate.Create(
            Guid.NewGuid(), "company", "Company", null, "Company",
            FormTemplateOwnerType.Company, companyId, Guid.NewGuid(), Now));
    }

    [Fact]
    public void VersionRequiresCanonicalOpaqueKeyAndValidEffectiveRange()
    {
        Assert.Throws<ArgumentException>(() => CreateVersion(storageObjectKey: "../template.pdf"));
        Assert.Throws<ArgumentException>(() => CreateVersion(
            effectiveFrom: new DateOnly(2026, 10, 2),
            effectiveTo: new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void VersionPublishesSupportedPdfWithServerOwnedMetadata()
    {
        var version = CreateVersion();
        var publisherId = Guid.NewGuid();

        version.Publish(publisherId, Now.AddMinutes(1));

        Assert.Equal(FormTemplateVersionStatus.Published, version.Status);
        Assert.Equal(publisherId, version.PublishedByUserId);
        Assert.Equal(Now.AddMinutes(1), version.PublishedAtUtc);
        Assert.Throws<InvalidOperationException>(() => version.Publish(publisherId, Now.AddMinutes(2)));
    }

    [Fact]
    public void UnsupportedVersionCannotBePublished()
    {
        var version = CreateVersion(formTechnology: FormTechnology.Unsupported);

        Assert.Throws<InvalidOperationException>(() => version.Publish(Guid.NewGuid(), Now));
    }

    [Theory]
    [InlineData(FormFieldPurpose.Signature)]
    [InlineData(FormFieldPurpose.Attestation)]
    [InlineData(FormFieldPurpose.Manual)]
    public void ManualSignatureAndAttestationMappingsRejectBinding(FormFieldPurpose purpose)
    {
        Assert.Throws<ArgumentException>(() => FormTemplateFieldMapping.Create(
            Guid.NewGuid(), Guid.NewGuid(), "field", FormFieldType.Text, purpose,
            "client.firstName", null, true, Now));
    }

    [Fact]
    public void DataMappingRequiresBinding()
    {
        Assert.Throws<ArgumentException>(() => FormTemplateFieldMapping.Create(
            Guid.NewGuid(), Guid.NewGuid(), "field", FormFieldType.Text,
            FormFieldPurpose.Data, null, null, true, Now));
    }

    [Fact]
    public void DataMappingStoresOnlyExplicitBindingMetadata()
    {
        var mapping = FormTemplateFieldMapping.Create(
            Guid.NewGuid(), Guid.NewGuid(), " form1.client.firstName ", FormFieldType.Text,
            FormFieldPurpose.Data, " client.firstName ", " uppercase ", true, Now);

        Assert.Equal("form1.client.firstName", mapping.PdfFieldName);
        Assert.Equal("client.firstName", mapping.BindingKey);
        Assert.Equal("uppercase", mapping.FormatKey);
        Assert.True(mapping.IsUserEditable);
    }

    private static FormTemplateVersion CreateVersion(
        string? storageObjectKey = null,
        FormTechnology formTechnology = FormTechnology.AcroForm,
        DateOnly? effectiveFrom = null,
        DateOnly? effectiveTo = null) =>
        FormTemplateVersion.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "DHS_6893F.pdf",
            storageObjectKey ?? Guid.NewGuid().ToString("N"),
            "application/pdf",
            3_415_779,
            new string('a', FormTemplateVersion.Sha256Length),
            7,
            formTechnology,
            effectiveFrom,
            effectiveTo,
            Guid.NewGuid(),
            Now);
}
