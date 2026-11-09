using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Features.ClientForms;
using CareComply.Domain.Clients;
using CareComply.Domain.Forms;
using CareComply.Infrastructure.Persistence;
using CareComply.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.Forms;

public sealed partial class DevelopmentFormTemplateInitializer(
    CareComplyDbContext context,
    IFormContentStore contentStore,
    IOptions<DevelopmentFormTemplateOptions> options,
    TimeProvider timeProvider)
{
    private static readonly Guid TemplateId = Guid.Parse("019a2f00-0000-7000-8000-000000000001");
    private static readonly Guid VersionId = Guid.Parse("019a2f00-0000-7000-8000-000000000002");
    private static readonly Guid DemoClientId = Guid.Parse("019a2f00-0000-7000-8000-000000000003");
    private static readonly Guid DemoPartyId = Guid.Parse("019a2f00-0000-7000-8000-000000000004");
    private const string ObjectKey = "019a2f00000070008000000000000005";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var sourcePath = options.Value.SourcePdfPath;
        if (string.IsNullOrWhiteSpace(sourcePath) || !Path.IsPathFullyQualified(sourcePath))
            throw new InvalidOperationException("A fully qualified development form-template source PDF path is required.");

        var administratorId = await context.Users.AsNoTracking()
            .Where(user => user.Email == "admin@carecomply.local")
            .Select(user => user.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (administratorId == Guid.Empty)
            throw new InvalidOperationException("Initialize the Development identity fixtures first.");

        var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken);
        if (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw new InvalidOperationException("The configured development form template is not a PDF.");

        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var existingVersion = await context.FormTemplateVersions.AsNoTracking()
            .SingleOrDefaultAsync(version => version.Id == VersionId, cancellationToken);
        if (existingVersion is null)
        {
            if (await context.FormTemplates.AnyAsync(template => template.Id == TemplateId, cancellationToken))
                throw new InvalidOperationException("The DHS-6893F development fixture is incomplete.");

            var now = timeProvider.GetUtcNow();
            var template = FormTemplate.Create(
                TemplateId, "DHS-6893F", "DHS-6893F", "DHS client form", "DHS",
                FormTemplateOwnerType.Platform, null, administratorId, now);
            var version = FormTemplateVersion.CreateDraft(
                VersionId, TemplateId, 1, "DHS_6893F.pdf", ObjectKey, "application/pdf",
                bytes.LongLength, sha256,
                CountPages(bytes), FormTechnology.AcroForm, null, null, administratorId, now);
            version.Publish(administratorId, now);

            context.FormTemplates.Add(template);
            context.FormTemplateVersions.Add(version);
            context.FormTemplateFieldMappings.AddRange(CreateMappings(now));
            await using var content = new MemoryStream(bytes, writable: false);
            await contentStore.WriteAsync(ObjectKey, content, cancellationToken);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                await contentStore.DeleteUncommittedAsync(ObjectKey, CancellationToken.None);
                throw;
            }
        }
        else
        {
            if (!string.Equals(existingVersion.Sha256, sha256, StringComparison.Ordinal)
                || existingVersion.SizeBytes != bytes.LongLength
                || existingVersion.Status != FormTemplateVersionStatus.Published)
            {
                throw new InvalidOperationException(
                    "The existing DHS-6893F development fixture does not match the configured PDF.");
            }

            try
            {
                await using var stored = await contentStore.OpenReadAsync(ObjectKey, cancellationToken);
            }
            catch (IOException exception) when (
                exception is FileNotFoundException or DirectoryNotFoundException)
            {
                await using var content = new MemoryStream(bytes, writable: false);
                await contentStore.WriteAsync(ObjectKey, content, cancellationToken);
            }
        }

        await EnsureSyntheticClientAsync(cancellationToken);
    }

    private async Task EnsureSyntheticClientAsync(CancellationToken cancellationToken)
    {
        if (await context.Clients.AnyAsync(client => client.Id == DemoClientId, cancellationToken)) return;

        var location = await context.CompanyLocations.AsNoTracking()
            .Where(item => item.CompanyId == TenantDefaults.DefaultCompanyId && item.IsActive)
            .OrderByDescending(item => item.IsMain)
            .ThenBy(item => item.Name)
            .Select(item => new { item.Id, item.Name })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Initialize the Development company/location fixtures first.");

        var now = timeProvider.GetUtcNow();
        var client = Client.Create(
            DemoClientId, TenantDefaults.DefaultCompanyId, "Jordan", "Lee", "Rivera", "Jordan",
            new DateOnly(1987, 4, 12), ClientGender.NonBinary, "DEMO-CF-001",
            new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 10), ClientStatus.Active, now);
        client.AssignLocation(location.Id, location.Name, now);
        client.ChangeContact(
            "jordan.rivera@example.test", "(612) 555-0142", null,
            "100 Test Avenue", null, "Minneapolis", "MN", "55401", "Hennepin", "United States", now);

        var party = new ClientContact(
            DemoPartyId, DemoClientId, ClientContactType.ResponsibleParty,
            "Morgan Rivera", "Parent", "(612) 555-0175", "morgan.rivera@example.test",
            true, false, null, null, null);
        party.UpdateResponsibleParty(
            "Morgan Rivera", "Parent", "(612) 555-0175", "morgan.rivera@example.test",
            "200 Example Street", false, false, true, "Morgan", null, "Rivera");

        context.Clients.Add(client);
        context.ClientContacts.Add(party);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static int CountPages(byte[] bytes)
    {
        var content = System.Text.Encoding.Latin1.GetString(bytes);
        var count = PdfPagePattern().Count(content);
        if (count <= 0) throw new InvalidOperationException("The PDF page count could not be determined.");
        return count;
    }

    private static FormTemplateFieldMapping[] CreateMappings(DateTimeOffset now)
    {
        var definitions = new (string PdfName, FormFieldType Type, FormFieldPurpose Purpose, string? Key)[]
        {
            ("form1[0].P1[0].sfRespParty[0].NameLast[0]", FormFieldType.Text, FormFieldPurpose.Data, ClientFormBindingKeys.ResponsiblePartyLastName),
            ("form1[0].P1[0].sfRespParty[0].NameFirst[0]", FormFieldType.Text, FormFieldPurpose.Data, ClientFormBindingKeys.ResponsiblePartyFirstName),
            ("form1[0].P1[0].sfRespParty[0].NameMiddle[0]", FormFieldType.Text, FormFieldPurpose.Data, ClientFormBindingKeys.ResponsiblePartyMiddleInitial),
            ("form1[0].P1[0].sfRespParty[0].Relationship[0]", FormFieldType.Text, FormFieldPurpose.Data, ClientFormBindingKeys.ResponsiblePartyRelationship),
            ("form1[0].P1[0].sfRespParty[0].NameLast[1]", FormFieldType.Text, FormFieldPurpose.Data, ClientFormBindingKeys.ClientLastName),
            ("form1[0].P1[0].sfRespParty[0].NameFirst[1]", FormFieldType.Text, FormFieldPurpose.Data, ClientFormBindingKeys.ClientFirstName),
            ("form1[0].P1[0].sfRespParty[0].NameMI[0]", FormFieldType.Text, FormFieldPurpose.Data, ClientFormBindingKeys.ClientMiddleInitial),
            ("form1[0].P1[0].sfRespParty[0].PMI[0]", FormFieldType.Text, FormFieldPurpose.Data, ClientFormBindingKeys.ClientPmiNumber),
            ("form1[0].P1[0].sfAgency[0].AgencyName[0]", FormFieldType.Text, FormFieldPurpose.Data, ClientFormBindingKeys.CompanyName),
            ("form1[0].P1[0].sfSignature[0].Phone[0]", FormFieldType.Text, FormFieldPurpose.Data, ClientFormBindingKeys.ResponsiblePartyPhone),
            ("form1[0].P1[0].sfSignature[0].Address[0]", FormFieldType.Text, FormFieldPurpose.Data, ClientFormBindingKeys.ResponsiblePartyAddress),
            ("form1[0].P1[0].sfSignature[0].City[0]", FormFieldType.Text, FormFieldPurpose.Data, ClientFormBindingKeys.ResponsiblePartyCity),
            ("form1[0].P1[0].sfSignature[0].StateID[0]", FormFieldType.Text, FormFieldPurpose.Data, ClientFormBindingKeys.ResponsiblePartyState),
            ("form1[0].P1[0].sfSignature[0].Zip[0]", FormFieldType.Text, FormFieldPurpose.Data, ClientFormBindingKeys.ResponsiblePartyPostalCode),
            ("form1[0].P1[0].sfAgree[0].StartDate[0]", FormFieldType.Text, FormFieldPurpose.Manual, null),
            ("form1[0].P1[0].sfAgree[0].EndDate[0]", FormFieldType.Text, FormFieldPurpose.Manual, null),
            ("form1[0].P1[0].sfProviderAssurance[0].TextField[0]", FormFieldType.Text, FormFieldPurpose.Manual, null),
            ("form1[0].P1[0].sfProviderAssurance[0].TextField[1]", FormFieldType.Text, FormFieldPurpose.Manual, null),
            ("form1[0].P1[0].sfProviderAssurance[0].TextField[2]", FormFieldType.Text, FormFieldPurpose.Manual, null),
            ("form1[0].P1[0].sfProviderAssurance[0].TextField[3]", FormFieldType.Text, FormFieldPurpose.Manual, null),
            ("form1[0].P1[0].sfProviderAssurance[0].TextField[4]", FormFieldType.Text, FormFieldPurpose.Manual, null),
            ("form1[0].P1[0].sfProviderAssurance[0].TextField[5]", FormFieldType.Text, FormFieldPurpose.Manual, null),
            ("form1[0].P1[0].sfProviderAssurance[0].TextField[6]", FormFieldType.Text, FormFieldPurpose.Manual, null),
            ("form1[0].P1[0].sfProviderAssurance[0].Comments[0]", FormFieldType.Text, FormFieldPurpose.Manual, null),
            ("form1[0].P1[0].sfAgency[0].AgencyContact[0]", FormFieldType.Text, FormFieldPurpose.Manual, null),
            ("form1[0].P1[0].sfAgency[0].Title[0]", FormFieldType.Text, FormFieldPurpose.Manual, null),
            ("form1[0].P1[0].sfAgency[0].DateField[0]", FormFieldType.Text, FormFieldPurpose.Manual, null),
            ("form1[0].P1[0].sfAcknowledgement[0].Checkbox[0]", FormFieldType.CheckBox, FormFieldPurpose.Attestation, null),
            ("form1[0].P1[0].sfAcknowledgement[0].Checkbox[1]", FormFieldType.CheckBox, FormFieldPurpose.Attestation, null),
            ("form1[0].P1[0].sfAcknowledgement[0].Checkbox[2]", FormFieldType.CheckBox, FormFieldPurpose.Attestation, null),
            ("form1[0].P1[0].sfAcknowledgement[0].Checkbox[3]", FormFieldType.CheckBox, FormFieldPurpose.Attestation, null),
            ("form1[0].P1[0].sfAcknowledgement[0].Checkbox[4]", FormFieldType.CheckBox, FormFieldPurpose.Attestation, null),
            ("form1[0].P1[0].sfSignature[0].DHS7368[0].SignatureCheck[0]", FormFieldType.CheckBox, FormFieldPurpose.Attestation, null),
            ("form1[0].P1[0].sfSignature[0].Signature[0]", FormFieldType.Text, FormFieldPurpose.Signature, null),
            ("form1[0].P1[0].sfSignature[0].Date[0]", FormFieldType.Text, FormFieldPurpose.Signature, null),
        };

        return definitions.Select((definition, index) => FormTemplateFieldMapping.Create(
            Guid.Parse($"019a2f00-0000-7000-8000-{(index + 100):D12}"),
            VersionId,
            definition.PdfName,
            definition.Type,
            definition.Purpose,
            definition.Key,
            null,
            true,
            now)).ToArray();
    }

    [GeneratedRegex(@"/Type\s*/Page(?!s)\b", RegexOptions.CultureInvariant)]
    private static partial Regex PdfPagePattern();
}
