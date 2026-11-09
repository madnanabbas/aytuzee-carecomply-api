using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Authorization;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Features.FormTemplates;

public sealed record ListFormTemplatesQuery
    : IQuery<FormTemplateListResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.FormTemplate.View;
}

public sealed class ListFormTemplatesQueryHandler(IFormTemplateAdministration administration)
    : IQueryHandler<ListFormTemplatesQuery, FormTemplateListResult>
{
    public async ValueTask<FormTemplateListResult> Handle(
        ListFormTemplatesQuery query,
        CancellationToken cancellationToken) =>
        new(await administration.ListAsync(cancellationToken), null);
}

public sealed record GetFormTemplateQuery(Guid TemplateId)
    : IQuery<FormTemplateDetailsResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.FormTemplate.View;
}

public sealed class GetFormTemplateQueryValidator : AbstractValidator<GetFormTemplateQuery>
{
    public GetFormTemplateQueryValidator() => RuleFor(query => query.TemplateId).NotEmpty();
}

public sealed class GetFormTemplateQueryHandler(IFormTemplateAdministration administration)
    : IQueryHandler<GetFormTemplateQuery, FormTemplateDetailsResult>
{
    public async ValueTask<FormTemplateDetailsResult> Handle(
        GetFormTemplateQuery query,
        CancellationToken cancellationToken) =>
        new(await administration.GetTemplateAsync(query.TemplateId, cancellationToken), null);
}

public sealed record GetFormTemplateVersionQuery(Guid TemplateId, Guid VersionId)
    : IQuery<FormTemplateVersionResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.FormTemplate.View;
}

public sealed class GetFormTemplateVersionQueryValidator
    : AbstractValidator<GetFormTemplateVersionQuery>
{
    public GetFormTemplateVersionQueryValidator()
    {
        RuleFor(query => query.TemplateId).NotEmpty();
        RuleFor(query => query.VersionId).NotEmpty();
    }
}

public sealed class GetFormTemplateVersionQueryHandler(IFormTemplateAdministration administration)
    : IQueryHandler<GetFormTemplateVersionQuery, FormTemplateVersionResult>
{
    public async ValueTask<FormTemplateVersionResult> Handle(
        GetFormTemplateVersionQuery query,
        CancellationToken cancellationToken) =>
        new(await administration.GetVersionAsync(
            query.TemplateId, query.VersionId, cancellationToken), null);
}

public sealed record GetFormTemplateFieldsQuery(Guid TemplateId, Guid VersionId)
    : IQuery<FormTemplateFieldsResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.FormTemplate.View;
}

public sealed class GetFormTemplateFieldsQueryValidator
    : AbstractValidator<GetFormTemplateFieldsQuery>
{
    public GetFormTemplateFieldsQueryValidator()
    {
        RuleFor(query => query.TemplateId).NotEmpty();
        RuleFor(query => query.VersionId).NotEmpty();
    }
}

public sealed class GetFormTemplateFieldsQueryHandler(IFormTemplateAdministration administration)
    : IQueryHandler<GetFormTemplateFieldsQuery, FormTemplateFieldsResult>
{
    public async ValueTask<FormTemplateFieldsResult> Handle(
        GetFormTemplateFieldsQuery query,
        CancellationToken cancellationToken) =>
        new(await administration.GetFieldsAsync(
            query.TemplateId, query.VersionId, cancellationToken), null);
}

public sealed record GetFormTemplateContentQuery(Guid TemplateId, Guid VersionId)
    : IQuery<FormTemplateContentResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.FormTemplate.View;
}

public sealed class GetFormTemplateContentQueryValidator
    : AbstractValidator<GetFormTemplateContentQuery>
{
    public GetFormTemplateContentQueryValidator()
    {
        RuleFor(query => query.TemplateId).NotEmpty();
        RuleFor(query => query.VersionId).NotEmpty();
    }
}

public sealed class GetFormTemplateContentQueryHandler(
    IFormTemplateAdministration administration,
    IFormContentStore contentStore)
    : IQueryHandler<GetFormTemplateContentQuery, FormTemplateContentResult>
{
    public async ValueTask<FormTemplateContentResult> Handle(
        GetFormTemplateContentQuery query,
        CancellationToken cancellationToken)
    {
        var version = await administration.GetVersionAsync(
            query.TemplateId, query.VersionId, cancellationToken);
        if (version is null)
        {
            return new(null, null, null, null);
        }

        var content = await contentStore.OpenReadAsync(
            version.StorageObjectKey, cancellationToken);
        return new(content, version.OriginalFileName, version.ContentType, null);
    }
}
