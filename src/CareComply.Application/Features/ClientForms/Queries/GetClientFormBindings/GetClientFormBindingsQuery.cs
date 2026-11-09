using System.Globalization;
using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Features.ClientForms.Queries.GetClientFormBindings;

public sealed record GetClientFormBindingsQuery(Guid ClientId, Guid TemplateVersionId)
    : IQuery<GetClientFormBindingsResult>, IRequirePermission,
        IAuditableRequest<GetClientFormBindingsResult>
{
    public string RequiredPermission => Permissions.ClientForm.View;

    public AuditEvent CreateAuditEvent(GetClientFormBindingsResult response) =>
        new(
            "ClientForm.BindingsAccessed",
            "ClientForm",
            ClientId.ToString("N"),
            response.Bindings is null ? AuditOutcome.Failed : AuditOutcome.Succeeded,
            response.Bindings is null
                ? AuditMetadata.Create(
                    ("reasonCode", "not-found"),
                    ("templateVersionId", TemplateVersionId.ToString("N")))
                : AuditMetadata.Create(
                    ("clientId", ClientId.ToString("N")),
                    ("templateKey", response.Bindings.TemplateKey),
                    ("version", response.Bindings.VersionNumber.ToString(
                        CultureInfo.InvariantCulture))));
}

public sealed record GetClientFormBindingsResult(
    ClientFormBindings? Bindings,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<GetClientFormBindingsResult>
{
    public static GetClientFormBindingsResult Succeeded(ClientFormBindings? bindings) =>
        new(bindings, null);

    public static GetClientFormBindingsResult Denied(AuthorizationFailure failure) =>
        new(null, failure);
}

public sealed class GetClientFormBindingsQueryHandler(IClientFormDirectory directory)
    : IQueryHandler<GetClientFormBindingsQuery, GetClientFormBindingsResult>
{
    public async ValueTask<GetClientFormBindingsResult> Handle(
        GetClientFormBindingsQuery query,
        CancellationToken cancellationToken) =>
        GetClientFormBindingsResult.Succeeded(
            await directory.GetBindingsAsync(
                query.ClientId,
                query.TemplateVersionId,
                cancellationToken));
}

public sealed class GetClientFormBindingsQueryValidator
    : AbstractValidator<GetClientFormBindingsQuery>
{
    public GetClientFormBindingsQueryValidator()
    {
        RuleFor(query => query.ClientId).NotEmpty();
        RuleFor(query => query.TemplateVersionId).NotEmpty();
    }
}
