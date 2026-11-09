using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Domain.Forms;
using FluentValidation;
using Mediator;
using System.Globalization;

namespace CareComply.Application.Features.FormTemplates;

public sealed class UploadFormTemplateCommand(
    Guid templateId,
    Guid versionId,
    string key,
    string name,
    string? description,
    string category,
    string originalFileName,
    DateOnly? effectiveFrom,
    DateOnly? effectiveTo,
    Stream content)
    : ICommand<FormTemplateOperationResult>, IRequirePermission,
        IRequirePlatformAdministrator, IAuditableRequest<FormTemplateOperationResult>
{
    public Guid TemplateId { get; } = templateId;
    public Guid VersionId { get; } = versionId;
    public string Key { get; } = key;
    public string Name { get; } = name;
    public string? Description { get; } = description;
    public string Category { get; } = category;
    public string OriginalFileName { get; } = originalFileName;
    public DateOnly? EffectiveFrom { get; } = effectiveFrom;
    public DateOnly? EffectiveTo { get; } = effectiveTo;
    public Stream Content { get; } = content;
    public string RequiredPermission => Permissions.FormTemplate.Manage;

    public AuditEvent CreateAuditEvent(FormTemplateOperationResult response) => new(
        "FormTemplate.Uploaded",
        "FormTemplate",
        TemplateId.ToString("N"),
        response.Status == FormTemplateOperationStatus.Succeeded
            ? AuditOutcome.Succeeded
            : AuditOutcome.Failed,
        AuditMetadata.Create(
            ("versionNumber", response.Version?.VersionNumber.ToString(CultureInfo.InvariantCulture) ?? "unavailable"),
            ("sha256Prefix", ShaPrefix(response.Version?.Sha256)),
            ("fieldCount", response.Version?.FieldCount.ToString(CultureInfo.InvariantCulture) ?? "0")));

    private static string ShaPrefix(string? value) =>
        value is { Length: >= 12 } ? value[..12] : "unavailable";
}

public sealed class UploadFormTemplateCommandValidator : AbstractValidator<UploadFormTemplateCommand>
{
    public UploadFormTemplateCommandValidator()
    {
        RuleFor(command => command.TemplateId).NotEmpty();
        RuleFor(command => command.VersionId).NotEmpty();
        RuleFor(command => command.Key).NotEmpty().MaximumLength(FormTemplate.MaximumKeyLength)
            .Matches("^[A-Za-z0-9][A-Za-z0-9._-]*$");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(FormTemplate.MaximumNameLength);
        RuleFor(command => command.Description).MaximumLength(FormTemplate.MaximumDescriptionLength);
        RuleFor(command => command.Category).NotEmpty().MaximumLength(FormTemplate.MaximumCategoryLength);
        RuleFor(command => command.OriginalFileName).NotEmpty()
            .MaximumLength(FormTemplateVersion.MaximumOriginalFileNameLength);
        RuleFor(command => command.Content).NotNull().Must(stream => stream.CanRead);
        RuleFor(command => command.EffectiveTo)
            .GreaterThanOrEqualTo(command => command.EffectiveFrom)
            .When(command => command.EffectiveFrom.HasValue && command.EffectiveTo.HasValue);
    }
}

public sealed class UploadFormTemplateCommandHandler(
    IFormTemplatePdfValidator validator,
    IFormTemplateAdministration administration,
    IFormContentStore contentStore,
    ICurrentActor currentActor,
    TimeProvider timeProvider)
    : ICommandHandler<UploadFormTemplateCommand, FormTemplateOperationResult>
{
    public async ValueTask<FormTemplateOperationResult> Handle(
        UploadFormTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command.Content, cancellationToken);
        if (validation.File is not { } file)
        {
            return FormTemplateOperationResult.Invalid(validation.Error);
        }

        await using (file)
        {
            var duplicate = await administration.FindDuplicateAsync(file.Sha256, cancellationToken);
            if (duplicate is not null)
            {
                return FormTemplateOperationResult.DuplicateContent(duplicate);
            }

            var actorUserId = currentActor.UserId
                ?? throw new InvalidOperationException("An authenticated actor is required.");
            var now = timeProvider.GetUtcNow();
            var objectKey = Guid.CreateVersion7().ToString("N");
            await using (var storedContent = file.OpenRead())
            {
                await contentStore.WriteAsync(objectKey, storedContent, cancellationToken);
            }

            try
            {
                var template = FormTemplate.Create(
                    command.TemplateId,
                    command.Key,
                    command.Name,
                    command.Description,
                    command.Category,
                    FormTemplateOwnerType.Platform,
                    null,
                    actorUserId,
                    now);
                var version = CreateVersion(
                    command.VersionId,
                    command.TemplateId,
                    1,
                    command,
                    file,
                    objectKey,
                    actorUserId,
                    now);
                var fields = CreateMappings(command.VersionId, file.Fields, now);
                var write = await administration.CreateTemplateAsync(
                    template, version, fields, cancellationToken);
                if (write.Status != FormTemplateWriteStatus.Succeeded)
                {
                    await contentStore.DeleteUncommittedAsync(objectKey, CancellationToken.None);
                }
                return FormTemplateOperationResult.FromWrite(write, file.Warnings);
            }
            catch
            {
                await contentStore.DeleteUncommittedAsync(objectKey, CancellationToken.None);
                throw;
            }
        }
    }

    internal static FormTemplateVersion CreateVersion(
        Guid versionId,
        Guid templateId,
        int versionNumber,
        UploadFormTemplateCommand command,
        IValidatedFormTemplateFile file,
        string objectKey,
        Guid actorUserId,
        DateTimeOffset now) => FormTemplateVersion.CreateDraft(
            versionId,
            templateId,
            versionNumber,
            SanitizeFileName(command.OriginalFileName),
            objectKey,
            "application/pdf",
            file.SizeBytes,
            file.Sha256,
            file.PageCount,
            file.Technology,
            command.EffectiveFrom,
            command.EffectiveTo,
            actorUserId,
            now);

    internal static FormTemplateFieldMapping[] CreateMappings(
        Guid versionId,
        IReadOnlyList<DiscoveredFormField> discoveredFields,
        DateTimeOffset now) => discoveredFields
        .Where(field => field.Type != FormFieldType.Button)
        .Select(field => FormTemplateFieldMapping.Create(
            Guid.CreateVersion7(),
            versionId,
            field.Name,
            field.Type,
            FormFieldPurpose.Manual,
            null,
            null,
            true,
            now))
        .ToArray();

    internal static string SanitizeFileName(string fileName)
    {
        var normalized = fileName.Replace('\\', '/');
        var safe = normalized[(normalized.LastIndexOf('/') + 1)..].Trim();
        return string.IsNullOrWhiteSpace(safe) ? "template.pdf" : safe;
    }
}
