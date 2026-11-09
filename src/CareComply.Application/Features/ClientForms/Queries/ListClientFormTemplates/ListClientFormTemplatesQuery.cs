using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Authorization;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Features.ClientForms.Queries.ListClientFormTemplates;

public sealed record ListClientFormTemplatesQuery(Guid ClientId)
    : IQuery<ListClientFormTemplatesResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.ClientForm.View;
}

public sealed record ListClientFormTemplatesResult(
    IReadOnlyList<ClientFormTemplateSummary>? Templates,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<ListClientFormTemplatesResult>
{
    public static ListClientFormTemplatesResult Succeeded(
        IReadOnlyList<ClientFormTemplateSummary>? templates) => new(templates, null);

    public static ListClientFormTemplatesResult Denied(AuthorizationFailure failure) =>
        new(null, failure);
}

public sealed class ListClientFormTemplatesQueryHandler(IClientFormDirectory directory)
    : IQueryHandler<ListClientFormTemplatesQuery, ListClientFormTemplatesResult>
{
    public async ValueTask<ListClientFormTemplatesResult> Handle(
        ListClientFormTemplatesQuery query,
        CancellationToken cancellationToken) =>
        ListClientFormTemplatesResult.Succeeded(
            await directory.ListAvailableAsync(query.ClientId, cancellationToken));
}

public sealed class ListClientFormTemplatesQueryValidator
    : AbstractValidator<ListClientFormTemplatesQuery>
{
    public ListClientFormTemplatesQueryValidator() =>
        RuleFor(query => query.ClientId).NotEmpty();
}
