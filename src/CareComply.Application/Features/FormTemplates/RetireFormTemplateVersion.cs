using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using FluentValidation;
using Mediator;
using System.Globalization;

namespace CareComply.Application.Features.FormTemplates;

public sealed record RetireFormTemplateVersionCommand(Guid TemplateId, Guid VersionId)
    : ICommand<FormTemplateOperationResult>, IRequirePermission,
        IRequirePlatformAdministrator, IAuditableRequest<FormTemplateOperationResult>
{
    public string RequiredPermission => Permissions.FormTemplate.Manage;

    public AuditEvent CreateAuditEvent(FormTemplateOperationResult response) => new(
        "FormTemplate.VersionRetired",
        "FormTemplate",
        TemplateId.ToString("N"),
        response.Status == FormTemplateOperationStatus.Succeeded
            ? AuditOutcome.Succeeded
            : AuditOutcome.Failed,
        AuditMetadata.Create(
            ("versionNumber", response.Version?.VersionNumber.ToString(CultureInfo.InvariantCulture) ?? "unavailable")));
}

public sealed class RetireFormTemplateVersionCommandValidator
    : AbstractValidator<RetireFormTemplateVersionCommand>
{
    public RetireFormTemplateVersionCommandValidator()
    {
        RuleFor(command => command.TemplateId).NotEmpty();
        RuleFor(command => command.VersionId).NotEmpty();
    }
}

public sealed class RetireFormTemplateVersionCommandHandler(
    IFormTemplateAdministration administration)
    : ICommandHandler<RetireFormTemplateVersionCommand, FormTemplateOperationResult>
{
    public async ValueTask<FormTemplateOperationResult> Handle(
        RetireFormTemplateVersionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await administration.RetireAsync(
            command.TemplateId, command.VersionId, cancellationToken);
        if (result.Status != FormTemplateRetireStatus.Succeeded)
        {
            return new FormTemplateOperationResult(
                result.Status switch
                {
                    FormTemplateRetireStatus.NotFound => FormTemplateOperationStatus.NotFound,
                    FormTemplateRetireStatus.InvalidState => FormTemplateOperationStatus.InvalidState,
                    _ => FormTemplateOperationStatus.Conflict,
                },
                null,
                null,
                [],
                FormTemplateFileValidationError.None,
                null,
                null);
        }

        var version = await administration.GetVersionAsync(
            command.TemplateId, command.VersionId, cancellationToken);
        return new FormTemplateOperationResult(
            FormTemplateOperationStatus.Succeeded,
            null,
            version,
            [],
            FormTemplateFileValidationError.None,
            null,
            null);
    }
}
