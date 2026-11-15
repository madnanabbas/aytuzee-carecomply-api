using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareComply.Application.Authorization;
using CareComply.Application.Auditing;
using CareComply.Domain.Forms;
using CareComply.Infrastructure.Auditing;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CareComply.Api.IntegrationTests;

public sealed class FormTemplateAdministrationIntegrationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task ReadsRequireViewAndMutationRequiresManagePlusPlatformAdministrator()
    {
        using var anonymous = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/form-templates")).StatusCode);
        using var anonymousUpload = await CreateUploadAsync($"anonymous-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync("/api/form-templates", anonymousUpload)).StatusCode);

        var denied = await factory.CreateUserAsync(UniqueEmail("template-denied"), Password);
        using var deniedClient = factory.CreateSecureClient();
        await SignInAsync(deniedClient, denied.Email!);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await deniedClient.GetAsync("/api/form-templates")).StatusCode);
        using var deniedUpload = await CreateUploadAsync($"denied-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await deniedClient.PostAsync("/api/form-templates", deniedUpload)).StatusCode);

        var viewer = await factory.CreateUserAsync(UniqueEmail("template-viewer"), Password);
        await factory.GrantPermissionAsync(viewer.Id, Permissions.FormTemplate.View);
        using var viewerClient = factory.CreateSecureClient();
        await SignInAsync(viewerClient, viewer.Email!);
        Assert.Equal(HttpStatusCode.OK,
            (await viewerClient.GetAsync("/api/form-templates")).StatusCode);

        var companyManager = await factory.CreateUserAsync(UniqueEmail("template-manager"), Password);
        await factory.GrantPermissionAsync(companyManager.Id, Permissions.FormTemplate.Manage);
        using var companyClient = factory.CreateSecureClient();
        await SignInAsync(companyClient, companyManager.Email!, includeAntiforgery: true);
        using var forbiddenUpload = await CreateUploadAsync("forbidden-template");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await companyClient.PostAsync("/api/form-templates", forbiddenUpload)).StatusCode);
    }

    [Fact]
    public async Task PlatformMutationRequiresAntiforgeryToken()
    {
        var actor = await CreatePlatformAdministratorAsync(Permissions.FormTemplate.Manage);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!);
        using var upload = await CreateUploadAsync($"no-csrf-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsync("/api/form-templates", upload)).StatusCode);
    }

    [Fact]
    public async Task PlatformUploadCreatesDraftMappingsWarningsAndSafeAuditThenRejectsDuplicate()
    {
        var actor = await CreatePlatformAdministratorAsync(
            Permissions.FormTemplate.Manage, Permissions.FormTemplate.View);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!, includeAntiforgery: true);
        var key = $"DHS-6893F-{Guid.NewGuid():N}";
        using var upload = await CreateUploadAsync(key);
        factory.FormTemplateLogProvider.Clear();

        var response = await client.PostAsync("/api/form-templates", upload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var versionJson = responseJson.RootElement.GetProperty("version");
        var templateId = responseJson.RootElement.GetProperty("template").GetProperty("id").GetGuid();
        var versionId = versionJson.GetProperty("id").GetGuid();
        Assert.Equal("Draft", versionJson.GetProperty("status").GetString());
        Assert.Equal(35, versionJson.GetProperty("fieldCount").GetInt32());
        var warningCodes = responseJson.RootElement.GetProperty("warnings").EnumerateArray()
            .Select(warning => warning.GetProperty("code").GetString()).ToArray();
        Assert.Contains("hybrid-xfa", warningCodes);
        Assert.Contains("adobe-version-check-scripts", warningCodes);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var version = await db.FormTemplateVersions.AsNoTracking()
                .SingleAsync(item => item.Id == versionId);
            Assert.Equal(FormTemplateVersionStatus.Draft, version.Status);
            Assert.Equal(FormTechnology.HybridXfaAcroForm, version.FormTechnology);
            Assert.Equal(35, await db.FormTemplateFieldMappings.CountAsync(
                field => field.FormTemplateVersionId == versionId));
            var mappings = await db.FormTemplateFieldMappings.AsNoTracking()
                .Where(field => field.FormTemplateVersionId == versionId).ToArrayAsync();
            Assert.All(mappings, field =>
            {
                Assert.Equal(FormFieldPurpose.Manual, field.FieldPurpose);
                Assert.Null(field.BindingKey);
                Assert.True(field.IsUserEditable);
                Assert.NotEqual(FormFieldType.Button, field.PdfFieldType);
            });
            var audit = await db.Set<AuditLog>().AsNoTracking()
                .SingleAsync(log => log.ActorUserId == actor.Id
                    && log.ResourceType == "FormTemplate"
                    && log.ResourceId == templateId.ToString("N"));
            using var metadata = JsonDocument.Parse(audit.MetadataJson!);
            Assert.Equal(
                ["fieldCount", "sha256Prefix", "versionNumber"],
                metadata.RootElement.EnumerateObject().Select(property => property.Name)
                    .Order(StringComparer.Ordinal).ToArray());
            Assert.DoesNotContain("DHS_6893F.pdf", audit.MetadataJson,
                StringComparison.OrdinalIgnoreCase);
            foreach (var mapping in mappings)
                Assert.DoesNotContain(mapping.PdfFieldName, audit.MetadataJson,
                    StringComparison.Ordinal);

            var logText = string.Join('\n', factory.FormTemplateLogProvider.Messages);
            Assert.DoesNotContain("DHS_6893F.pdf", logText, StringComparison.OrdinalIgnoreCase);
            foreach (var mapping in mappings)
                Assert.DoesNotContain(mapping.PdfFieldName, logText, StringComparison.Ordinal);
        }

        using var duplicate = await CreateUploadAsync($"DUP-{Guid.NewGuid():N}");
        var duplicateResponse = await client.PostAsync("/api/form-templates", duplicate);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        using var duplicateJson = JsonDocument.Parse(await duplicateResponse.Content.ReadAsStringAsync());
        Assert.Equal(templateId,
            duplicateJson.RootElement.GetProperty("templateId").GetGuid());

        var list = await client.GetFromJsonAsync<JsonElement>("/api/form-templates");
        var summary = Assert.Single(
            list.EnumerateArray(), item => item.GetProperty("id").GetGuid() == templateId);
        Assert.Equal(
            versionJson.GetProperty("uploadedAtUtc").GetDateTimeOffset(),
            summary.GetProperty("latestVersionUploadedAtUtc").GetDateTimeOffset());
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync($"/api/form-templates/{templateId}/versions/{versionId}/fields")).StatusCode);
        var content = await client.GetAsync(
            $"/api/form-templates/{templateId}/versions/{versionId}/content");
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Equal("no-store", content.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", content.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(await File.ReadAllBytesAsync(FindSample()),
            await content.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task VersionUploadsIncrementNumbersAndStoredBytesRemainImmutable()
    {
        var actor = await CreatePlatformAdministratorAsync(
            Permissions.FormTemplate.Manage, Permissions.FormTemplate.View);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!, includeAntiforgery: true);
        var firstBytes = CreateSafePdf($"VersionOne{Guid.NewGuid():N}");
        var expectedFirstBytes = firstBytes.ToArray();
        var secondBytes = CreateSafePdf($"VersionTwo{Guid.NewGuid():N}");
        using var upload = await CreateUploadAsync(
            $"versioned-{Guid.NewGuid():N}", firstBytes, "first.pdf");

        var firstResponse = await client.PostAsync("/api/form-templates", upload);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var first = await ReadMutationAsync(firstResponse);
        Assert.Equal(1, first.VersionNumber);

        Array.Fill(firstBytes, (byte)0);
        var storedFirst = await client.GetByteArrayAsync(
            $"/api/form-templates/{first.TemplateId}/versions/{first.VersionId}/content");
        Assert.NotEqual(firstBytes, storedFirst);
        Assert.Equal(expectedFirstBytes, storedFirst);

        using var versionUpload = CreateVersionUpload(secondBytes, "second.pdf");
        var secondResponse = await client.PostAsync(
            $"/api/form-templates/{first.TemplateId}/versions", versionUpload);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var second = await ReadMutationAsync(secondResponse);
        Assert.Equal(2, second.VersionNumber);
        Assert.Equal(secondBytes, await client.GetByteArrayAsync(
            $"/api/form-templates/{first.TemplateId}/versions/{second.VersionId}/content"));
    }

    [Fact]
    public async Task PublishedVersionCanRetireButDraftCannot()
    {
        var actor = await CreatePlatformAdministratorAsync(Permissions.FormTemplate.Manage);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!, includeAntiforgery: true);
        using var upload = await CreateUploadAsync(
            $"retire-{Guid.NewGuid():N}", CreateSafePdf($"Retire{Guid.NewGuid():N}"));
        var uploadResponse = await client.PostAsync("/api/form-templates", upload);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var created = await ReadMutationAsync(uploadResponse);

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(
            $"/api/form-templates/{created.TemplateId}/versions/{created.VersionId}/retire", null)).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var version = await db.FormTemplateVersions.SingleAsync(item => item.Id == created.VersionId);
            version.Publish(actor.Id, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(
            $"/api/form-templates/{created.TemplateId}/versions/{created.VersionId}/retire", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(
            $"/api/form-templates/{created.TemplateId}/versions/{created.VersionId}/retire", null)).StatusCode);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.Equal(FormTemplateVersionStatus.Retired,
            (await verificationDb.FormTemplateVersions.AsNoTracking()
                .SingleAsync(item => item.Id == created.VersionId)).Status);
    }

    [Fact]
    public async Task PersistenceConflictDeletesUncommittedContentWithoutOrphan()
    {
        var actor = await CreatePlatformAdministratorAsync(Permissions.FormTemplate.Manage);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!, includeAntiforgery: true);
        var key = $"conflict-{Guid.NewGuid():N}";
        using var firstUpload = await CreateUploadAsync(
            key, CreateSafePdf($"First{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsync("/api/form-templates", firstUpload)).StatusCode);
        var storedFileCount = Directory.GetFiles(factory.FormContentRoot, "*.pdf").Length;

        using var conflictingUpload = await CreateUploadAsync(
            key, CreateSafePdf($"Second{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsync("/api/form-templates", conflictingUpload)).StatusCode);

        Assert.Equal(storedFileCount, Directory.GetFiles(factory.FormContentRoot, "*.pdf").Length);
    }

    [Fact]
    public async Task PlatformAdminCanReviewPreviewAndPublishMappingsAndPreviousVersionAutoRetires()
    {
        var actor = await CreatePlatformAdministratorAsync(
            Permissions.FormTemplate.Manage, Permissions.FormTemplate.View);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!, includeAntiforgery: true);
        using var upload = await CreateUploadAsync(
            $"mapping-{Guid.NewGuid():N}", CreateSafePdf("ClientFirstName"));
        var uploadResponse = await client.PostAsync("/api/form-templates", upload);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var first = await ReadMutationAsync(uploadResponse);

        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/form-templates/binding-catalog");
        var catalogText = catalog.GetRawText();
        Assert.DoesNotContain("ssn", catalogText, StringComparison.OrdinalIgnoreCase);

        var firstMappings = await client.GetFromJsonAsync<JsonElement>(
            $"/api/form-templates/{first.TemplateId}/versions/{first.VersionId}/mappings");
        var firstField = firstMappings.GetProperty("mappings")[0];
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(
            $"/api/form-templates/{first.TemplateId}/versions/{first.VersionId}/mappings",
            new
            {
                rowVersion = "not-base64",
                mappings = new[]
                {
                    new { id = firstField.GetProperty("id").GetGuid(), fieldPurpose = "Manual",
                        bindingKey = (string?)null },
                },
            })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(
            $"/api/form-templates/{first.TemplateId}/versions/{first.VersionId}/mappings",
            new
            {
                rowVersion = firstMappings.GetProperty("rowVersion").GetString(),
                mappings = new[]
                {
                    new { id = firstField.GetProperty("id").GetGuid(), fieldPurpose = "FuturePurpose",
                        bindingKey = (string?)null },
                },
            })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            $"/api/form-templates/{first.TemplateId}/versions/{first.VersionId}/publish",
            new { rowVersion = firstMappings.GetProperty("rowVersion").GetString(),
                acknowledgeValidationWarnings = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(
            $"/api/form-templates/{first.TemplateId}/versions/{first.VersionId}/mappings",
            new { rowVersion = firstMappings.GetProperty("rowVersion").GetString(),
                mappings = Array.Empty<object>() })).StatusCode);
        var invalidResponse = await client.PutAsJsonAsync(
            $"/api/form-templates/{first.TemplateId}/versions/{first.VersionId}/mappings",
            new
            {
                rowVersion = firstMappings.GetProperty("rowVersion").GetString(),
                mappings = new[]
                {
                    new { id = firstField.GetProperty("id").GetGuid(), fieldPurpose = "Data",
                        bindingKey = "client.ssn" },
                },
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        var savedResponse = await client.PutAsJsonAsync(
            $"/api/form-templates/{first.TemplateId}/versions/{first.VersionId}/mappings",
            new
            {
                rowVersion = firstMappings.GetProperty("rowVersion").GetString(),
                mappings = new[]
                {
                    new { id = firstField.GetProperty("id").GetGuid(), fieldPurpose = "Data",
                        bindingKey = "client.firstName" },
                },
            });
        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);
        var saved = await savedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(
            $"/api/form-templates/{first.TemplateId}/versions/{first.VersionId}/mappings",
            new
            {
                rowVersion = firstMappings.GetProperty("rowVersion").GetString(),
                mappings = new[]
                {
                    new { id = firstField.GetProperty("id").GetGuid(), fieldPurpose = "Manual",
                        bindingKey = (string?)null },
                },
            })).StatusCode);

        var preview = await client.GetFromJsonAsync<JsonElement>(
            $"/api/form-templates/{first.TemplateId}/versions/{first.VersionId}/preview");
        Assert.True(preview.GetProperty("isSynthetic").GetBoolean());
        Assert.Equal("Avery", preview.GetProperty("values")[0].GetProperty("value").GetString());

        var publishResponse = await client.PostAsJsonAsync(
            $"/api/form-templates/{first.TemplateId}/versions/{first.VersionId}/publish",
            new { rowVersion = saved.GetProperty("rowVersion").GetString(), acknowledgeValidationWarnings = false });
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        Assert.Equal("Published", (await publishResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(
            $"/api/form-templates/{first.TemplateId}/versions/{first.VersionId}/mappings",
            new
            {
                rowVersion = saved.GetProperty("rowVersion").GetString(),
                mappings = new[]
                {
                    new { id = firstField.GetProperty("id").GetGuid(), fieldPurpose = "Manual",
                        bindingKey = (string?)null },
                },
            })).StatusCode);

        using var secondUpload = CreateVersionUpload(
            CreateSafePdf($"Second{Guid.NewGuid():N}"), "second.pdf");
        var secondResponse = await client.PostAsync(
            $"/api/form-templates/{first.TemplateId}/versions", secondUpload);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var second = await ReadMutationAsync(secondResponse);
        var secondMappings = await client.GetFromJsonAsync<JsonElement>(
            $"/api/form-templates/{first.TemplateId}/versions/{second.VersionId}/mappings");
        var secondField = secondMappings.GetProperty("mappings")[0];
        var secondSavedResponse = await client.PutAsJsonAsync(
            $"/api/form-templates/{first.TemplateId}/versions/{second.VersionId}/mappings",
            new
            {
                rowVersion = secondMappings.GetProperty("rowVersion").GetString(),
                mappings = new[]
                {
                    new { id = secondField.GetProperty("id").GetGuid(), fieldPurpose = "Manual",
                        bindingKey = (string?)null },
                },
            });
        Assert.Equal(HttpStatusCode.OK, secondSavedResponse.StatusCode);
        var secondSaved = await secondSavedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            $"/api/form-templates/{first.TemplateId}/versions/{second.VersionId}/publish",
            new { rowVersion = secondSaved.GetProperty("rowVersion").GetString(), acknowledgeValidationWarnings = false }))
            .StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
        Assert.Equal(FormTemplateVersionStatus.Retired,
            (await db.FormTemplateVersions.AsNoTracking().SingleAsync(item => item.Id == first.VersionId)).Status);
        Assert.Equal(FormTemplateVersionStatus.Published,
            (await db.FormTemplateVersions.AsNoTracking().SingleAsync(item => item.Id == second.VersionId)).Status);
        var auditRows = await db.Set<AuditLog>().AsNoTracking()
            .Where(item => item.ActorUserId == actor.Id
                && item.ResourceId == first.TemplateId.ToString("N")
                && item.Outcome == AuditOutcome.Succeeded
                && (item.Action == "FormTemplate.MappingsSaved"
                    || item.Action == "FormTemplate.Published"))
            .ToArrayAsync();
        Assert.Equal(4, auditRows.Length);
        Assert.All(auditRows, row =>
        {
            Assert.DoesNotContain("ClientFirstName", row.MetadataJson ?? string.Empty,
                StringComparison.Ordinal);
            Assert.DoesNotContain("Avery", row.MetadataJson ?? string.Empty,
                StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task MappingEndpointsRequireAuthenticationManagePermissionAndPlatformAdministrator()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        using var anonymous = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(
            $"/api/form-templates/{templateId}/versions/{versionId}/mappings")).StatusCode);

        var companyManager = await factory.CreateUserAsync(UniqueEmail("mapping-manager"), Password);
        await factory.GrantPermissionAsync(companyManager.Id, Permissions.FormTemplate.Manage);
        using var companyClient = factory.CreateSecureClient();
        await SignInAsync(companyClient, companyManager.Email!);
        Assert.Equal(HttpStatusCode.Forbidden, (await companyClient.GetAsync(
            $"/api/form-templates/{templateId}/versions/{versionId}/mappings")).StatusCode);
    }

    [Fact]
    public async Task ValidationWarningsRequireExplicitAcknowledgementBeforePublishing()
    {
        var actor = await CreatePlatformAdministratorAsync(Permissions.FormTemplate.Manage);
        using var client = factory.CreateSecureClient();
        await SignInAsync(client, actor.Email!, includeAntiforgery: true);
        using var upload = await CreateUploadAsync(
            $"warning-{Guid.NewGuid():N}", CreateSafePdf($"Warning{Guid.NewGuid():N}"));
        var uploaded = await client.PostAsync("/api/form-templates", upload);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var created = await ReadMutationAsync(uploaded);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            await db.FormTemplateVersions.Where(item => item.Id == created.VersionId)
                .ExecuteUpdateAsync(update => update.SetProperty(
                    item => item.RequiresWarningAcknowledgement, true));
        }
        var mappings = await client.GetFromJsonAsync<JsonElement>(
            $"/api/form-templates/{created.TemplateId}/versions/{created.VersionId}/mappings");
        var updates = mappings.GetProperty("mappings").EnumerateArray()
            .Select(item => new
            {
                id = item.GetProperty("id").GetGuid(),
                fieldPurpose = "Manual",
                bindingKey = (string?)null,
            }).ToArray();
        var savedResponse = await client.PutAsJsonAsync(
            $"/api/form-templates/{created.TemplateId}/versions/{created.VersionId}/mappings",
            new { rowVersion = mappings.GetProperty("rowVersion").GetString(), mappings = updates });
        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);
        var saved = await savedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var publishUrl = $"/api/form-templates/{created.TemplateId}/versions/{created.VersionId}/publish";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(publishUrl,
            new { rowVersion = saved.GetProperty("rowVersion").GetString(), acknowledgeValidationWarnings = false }))
            .StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(publishUrl,
            new { rowVersion = saved.GetProperty("rowVersion").GetString(), acknowledgeValidationWarnings = true }))
            .StatusCode);
    }

    private async Task<ApplicationUser> CreatePlatformAdministratorAsync(params string[] permissions)
    {
        var actor = await factory.CreateUserAsync(UniqueEmail("platform-template"), Password);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareComplyDbContext>();
            var stored = await db.Users.SingleAsync(user => user.Id == actor.Id);
            stored.CompanyId = null;
            stored.IsPlatformAdministrator = true;
            await db.SaveChangesAsync();
        }
        foreach (var permission in permissions) await factory.GrantPermissionAsync(actor.Id, permission);
        return actor;
    }

    private static async Task SignInAsync(
        HttpClient client,
        string email,
        bool includeAntiforgery = false)
    {
        var login = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        if (!includeAntiforgery) return;
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add(
            "X-XSRF-TOKEN", csrf.GetProperty("requestToken").GetString());
    }

    private static async Task<MultipartFormDataContent> CreateUploadAsync(
        string key,
        byte[]? suppliedBytes = null,
        string fileName = "DHS_6893F.pdf")
    {
        var multipart = new MultipartFormDataContent
        {
            { new StringContent(key), "Key" },
            { new StringContent("Synthetic DHS 6893F"), "Name" },
            { new StringContent("Synthetic integration fixture"), "Description" },
            { new StringContent("DHS"), "Category" },
        };
        var bytes = suppliedBytes ?? await File.ReadAllBytesAsync(FindSample());
        multipart.Add(new ByteArrayContent(bytes), "File", fileName);
        return multipart;
    }

    private static MultipartFormDataContent CreateVersionUpload(byte[] bytes, string fileName)
    {
        var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(bytes), "File", fileName);
        return multipart;
    }

    private static byte[] CreateSafePdf(string fieldName) =>
        FormTemplatePdfValidationTests.CreateSingleFieldPdf("<< /S /ResetForm >>", fieldName);

    private static async Task<MutationDetails> ReadMutationAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var version = json.RootElement.GetProperty("version");
        return new(
            json.RootElement.GetProperty("template").GetProperty("id").GetGuid(),
            version.GetProperty("id").GetGuid(),
            version.GetProperty("versionNumber").GetInt32());
    }

    private sealed record MutationDetails(
        Guid TemplateId,
        Guid VersionId,
        int VersionNumber);

    private static string UniqueEmail(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}@example.test";

    private static string FindSample()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "form-templates", "DHS_6893F.pdf");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("The synthetic DHS_6893F.pdf test fixture was not found.");
    }
}
