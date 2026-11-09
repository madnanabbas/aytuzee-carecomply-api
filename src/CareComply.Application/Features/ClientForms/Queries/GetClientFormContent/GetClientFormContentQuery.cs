using System.Globalization;
using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Features.ClientForms.Queries.GetClientFormContent;

public sealed record GetClientFormContentQuery(Guid ClientId, Guid TemplateVersionId)
    : IQuery<GetClientFormContentResult>, IRequirePermission,
        IAuditableRequest<GetClientFormContentResult>
{
    public string RequiredPermission => Permissions.ClientForm.View;

    public AuditEvent CreateAuditEvent(GetClientFormContentResult response) =>
        ClientFormAccessAudit.Create(
            "ClientForm.ContentAccessed",
            ClientId,
            TemplateVersionId,
            response.Content);
}

public sealed record GetClientFormContentResult(
    ClientFormContentDescriptor? Content,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<GetClientFormContentResult>
{
    public static GetClientFormContentResult Succeeded(ClientFormContentDescriptor? content) =>
        new(content, null);

    public static GetClientFormContentResult Denied(AuthorizationFailure failure) =>
        new(null, failure);
}

public sealed class GetClientFormContentQueryHandler(IClientFormDirectory directory)
    : IQueryHandler<GetClientFormContentQuery, GetClientFormContentResult>
{
    public async ValueTask<GetClientFormContentResult> Handle(
        GetClientFormContentQuery query,
        CancellationToken cancellationToken) =>
        GetClientFormContentResult.Succeeded(
            await directory.GetContentDescriptorAsync(
                query.ClientId,
                query.TemplateVersionId,
                cancellationToken));
}

public sealed class GetClientFormContentQueryValidator
    : AbstractValidator<GetClientFormContentQuery>
{
    public GetClientFormContentQueryValidator()
    {
        RuleFor(query => query.ClientId).NotEmpty();
        RuleFor(query => query.TemplateVersionId).NotEmpty();
    }
}

internal static class ClientFormAccessAudit
{
    public static AuditEvent Create(
        string action,
        Guid clientId,
        Guid requestedVersionId,
        ClientFormContentDescriptor? content) =>
        new(
            action,
            "ClientForm",
            clientId.ToString("N"),
            content is null ? AuditOutcome.Failed : AuditOutcome.Succeeded,
            content is null
                ? AuditMetadata.Create(
                    ("reasonCode", "not-found"),
                    ("templateVersionId", requestedVersionId.ToString("N")))
                : AuditMetadata.Create(
                    ("clientId", clientId.ToString("N")),
                    ("templateKey", content.TemplateKey),
                    ("version", content.VersionNumber.ToString(CultureInfo.InvariantCulture))));
}
