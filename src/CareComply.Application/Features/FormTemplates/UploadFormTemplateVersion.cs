using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Domain.Forms;
using FluentValidation;
using Mediator;
using System.Globalization;

namespace CareComply.Application.Features.FormTemplates;

public sealed class UploadFormTemplateVersionCommand(
    Guid templateId,
    Guid versionId,
    string originalFileName,
    DateOnly? effectiveFrom,
    DateOnly? effectiveTo,
    Stream content)
    : ICommand<FormTemplateOperationResult>, IRequirePermission,
        IRequirePlatformAdministrator, IAuditableRequest<FormTemplateOperationResult>
{
    public Guid TemplateId { get; } = templateId;
    public Guid VersionId { get; } = versionId;
    public string OriginalFileName { get; } = originalFileName;
    public DateOnly? EffectiveFrom { get; } = effectiveFrom;
    public DateOnly? EffectiveTo { get; } = effectiveTo;
    public Stream Content { get; } = content;
    public string RequiredPermission => Permissions.FormTemplate.Manage;

    public AuditEvent CreateAuditEvent(FormTemplateOperationResult response) => new(
        "FormTemplate.VersionUploaded",
        "FormTemplate",
        TemplateId.ToString("N"),
        response.Status == FormTemplateOperationStatus.Succeeded
            ? AuditOutcome.Succeeded
            : AuditOutcome.Failed,
        AuditMetadata.Create(
            ("versionNumber", response.Version?.VersionNumber.ToString(CultureInfo.InvariantCulture) ?? "unavailable"),
            ("sha256Prefix", response.Version?.Sha256 is { Length: >= 12 } sha ? sha[..12] : "unavailable"),
            ("fieldCount", response.Version?.FieldCount.ToString(CultureInfo.InvariantCulture) ?? "0")));
}

public sealed class UploadFormTemplateVersionCommandValidator
    : AbstractValidator<UploadFormTemplateVersionCommand>
{
    public UploadFormTemplateVersionCommandValidator()
    {
        RuleFor(command => command.TemplateId).NotEmpty();
        RuleFor(command => command.VersionId).NotEmpty();
        RuleFor(command => command.OriginalFileName).NotEmpty()
            .MaximumLength(FormTemplateVersion.MaximumOriginalFileNameLength);
        RuleFor(command => command.Content).NotNull().Must(stream => stream.CanRead);
        RuleFor(command => command.EffectiveTo)
            .GreaterThanOrEqualTo(command => command.EffectiveFrom)
            .When(command => command.EffectiveFrom.HasValue && command.EffectiveTo.HasValue);
    }
}

public sealed class UploadFormTemplateVersionCommandHandler(
    IFormTemplatePdfValidator validator,
    IFormTemplateAdministration administration,
    IFormContentStore contentStore,
    ICurrentActor currentActor,
    TimeProvider timeProvider)
    : ICommandHandler<UploadFormTemplateVersionCommand, FormTemplateOperationResult>
{
    public async ValueTask<FormTemplateOperationResult> Handle(
        UploadFormTemplateVersionCommand command,
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
                var write = await administration.CreateVersionAsync(
                    command.TemplateId,
                    versionNumber => FormTemplateVersion.CreateDraft(
                        command.VersionId,
                        command.TemplateId,
                        versionNumber,
                        UploadFormTemplateCommandHandler.SanitizeFileName(command.OriginalFileName),
                        objectKey,
                        "application/pdf",
                        file.SizeBytes,
                        file.Sha256,
                        file.PageCount,
                        file.Technology,
                        command.EffectiveFrom,
                        command.EffectiveTo,
                        actorUserId,
                        now,
                        file.Warnings.Count > 0),
                    versionId => UploadFormTemplateCommandHandler.CreateMappings(
                        versionId, file.Fields, now),
                    cancellationToken);
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
}
