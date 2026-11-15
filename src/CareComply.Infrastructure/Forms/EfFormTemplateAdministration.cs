using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Domain.Forms;
using CareComply.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Forms;

public sealed class EfFormTemplateAdministration(
    CareComplyDbContext dbContext,
    ITenantScopeAccessor tenantScopeAccessor) : IFormTemplateAdministration
{
    public async Task<FormTemplateDuplicate?> FindDuplicateAsync(
        string sha256,
        CancellationToken cancellationToken) =>
        await (from version in dbContext.FormTemplateVersions.AsNoTracking()
               join template in dbContext.FormTemplates.AsNoTracking()
                   on version.FormTemplateId equals template.Id
               where version.Sha256 == sha256
               orderby version.UploadedAtUtc
               select new FormTemplateDuplicate(template.Id, template.Key, version.VersionNumber))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FormTemplateWriteResult> CreateTemplateAsync(
        FormTemplate formTemplate,
        FormTemplateVersion version,
        IReadOnlyList<FormTemplateFieldMapping> fields,
        CancellationToken cancellationToken)
    {
        dbContext.Add(formTemplate);
        dbContext.Add(version);
        dbContext.AddRange(fields);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            return new(FormTemplateWriteStatus.DuplicateKey, null, null);
        }

        return new(
            FormTemplateWriteStatus.Succeeded,
            await GetTemplateAsync(formTemplate.Id, cancellationToken),
            await GetVersionAsync(formTemplate.Id, version.Id, cancellationToken));
    }

    public async Task<FormTemplateWriteResult> CreateVersionAsync(
        Guid templateId,
        Func<int, FormTemplateVersion> versionFactory,
        Func<Guid, IReadOnlyList<FormTemplateFieldMapping>> fieldFactory,
        CancellationToken cancellationToken)
    {
        var template = await dbContext.FormTemplates
            .SingleOrDefaultAsync(item => item.Id == templateId, cancellationToken);
        if (template is null || template.OwnerType != FormTemplateOwnerType.Platform)
            return new(FormTemplateWriteStatus.TemplateNotFound, null, null);

        var nextNumber = (await dbContext.FormTemplateVersions
            .Where(version => version.FormTemplateId == templateId)
            .MaxAsync(version => (int?)version.VersionNumber, cancellationToken) ?? 0) + 1;
        var version = versionFactory(nextNumber);
        dbContext.Add(version);
        dbContext.AddRange(fieldFactory(version.Id));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            return new(FormTemplateWriteStatus.Conflict, null, null);
        }

        return new(
            FormTemplateWriteStatus.Succeeded,
            await GetTemplateAsync(templateId, cancellationToken),
            await GetVersionAsync(templateId, version.Id, cancellationToken));
    }

    public async Task<IReadOnlyList<FormTemplateSummary>> ListAsync(
        CancellationToken cancellationToken)
    {
        var scope = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (scope is null) return [];

        var query = VisibleTemplates(scope);
        return await query
            .OrderBy(template => template.Name)
            .Select(template => new FormTemplateSummary(
                template.Id,
                template.Key,
                template.Name,
                template.Category,
                template.OwnerType,
                template.CompanyId,
                template.IsActive,
                dbContext.FormTemplateVersions.Count(version => version.FormTemplateId == template.Id),
                dbContext.FormTemplateVersions
                    .Where(version => version.FormTemplateId == template.Id)
                    .OrderByDescending(version => version.VersionNumber)
                    .Select(version => (int?)version.VersionNumber)
                    .FirstOrDefault(),
                dbContext.FormTemplateVersions
                    .Where(version => version.FormTemplateId == template.Id)
                    .OrderByDescending(version => version.VersionNumber)
                    .Select(version => (FormTemplateVersionStatus?)version.Status)
                    .FirstOrDefault(),
                dbContext.FormTemplateVersions
                    .Where(version => version.FormTemplateId == template.Id)
                    .OrderByDescending(version => version.VersionNumber)
                    .Select(version => (DateTimeOffset?)version.UploadedAtUtc)
                    .FirstOrDefault()))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<FormTemplateDetails?> GetTemplateAsync(
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var scope = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (scope is null) return null;
        var template = await VisibleTemplates(scope)
            .SingleOrDefaultAsync(item => item.Id == templateId, cancellationToken);
        if (template is null) return null;
        var versions = await dbContext.FormTemplateVersions.AsNoTracking()
            .Where(version => version.FormTemplateId == templateId)
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => MapVersion(version, dbContext.FormTemplateFieldMappings
                .Count(field => field.FormTemplateVersionId == version.Id)))
            .ToArrayAsync(cancellationToken);
        return new(template.Id, template.Key, template.Name, template.Description,
            template.Category, template.OwnerType, template.CompanyId, template.IsActive,
            template.CreatedAtUtc, versions);
    }

    public async Task<FormTemplateVersionDetails?> GetVersionAsync(
        Guid templateId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var scope = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (scope is null || !await VisibleTemplates(scope).AnyAsync(
                template => template.Id == templateId, cancellationToken)) return null;
        return await dbContext.FormTemplateVersions.AsNoTracking()
            .Where(version => version.FormTemplateId == templateId && version.Id == versionId)
            .Select(version => MapVersion(version, dbContext.FormTemplateFieldMappings
                .Count(field => field.FormTemplateVersionId == version.Id)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FormTemplateFieldDetails>?> GetFieldsAsync(
        Guid templateId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        if (await GetVersionAsync(templateId, versionId, cancellationToken) is null) return null;
        return await dbContext.FormTemplateFieldMappings.AsNoTracking()
            .Where(field => field.FormTemplateVersionId == versionId)
            .OrderBy(field => field.PdfFieldName)
            .Select(field => new FormTemplateFieldDetails(field.Id, field.PdfFieldName,
                field.PdfFieldType, field.FieldPurpose, field.BindingKey, field.FormatKey,
                field.IsUserEditable))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<FormTemplateRetireResult> RetireAsync(
        Guid templateId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var template = await dbContext.FormTemplates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == templateId, cancellationToken);
        if (template is null || template.OwnerType != FormTemplateOwnerType.Platform)
            return new(FormTemplateRetireStatus.NotFound, templateId, versionId, null);
        var version = await dbContext.FormTemplateVersions.SingleOrDefaultAsync(
            item => item.FormTemplateId == templateId && item.Id == versionId, cancellationToken);
        if (version is null)
            return new(FormTemplateRetireStatus.NotFound, templateId, versionId, null);
        try
        {
            version.Retire();
        }
        catch (InvalidOperationException)
        {
            return new(FormTemplateRetireStatus.InvalidState, templateId, versionId, version.VersionNumber);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new(FormTemplateRetireStatus.Succeeded, templateId, versionId, version.VersionNumber);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(FormTemplateRetireStatus.Conflict, templateId, versionId, version.VersionNumber);
        }
    }

    public async Task<FormTemplateMappingSet?> GetMappingSetAsync(
        Guid templateId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var template = await dbContext.FormTemplates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == templateId, cancellationToken);
        if (template is null || template.OwnerType != FormTemplateOwnerType.Platform) return null;
        var version = await dbContext.FormTemplateVersions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.FormTemplateId == templateId && item.Id == versionId,
                cancellationToken);
        if (version is null) return null;
        var mappings = await dbContext.FormTemplateFieldMappings.AsNoTracking()
            .Where(item => item.FormTemplateVersionId == versionId)
            .OrderBy(item => item.PdfFieldName)
            .Select(item => new FormTemplateFieldDetails(
                item.Id, item.PdfFieldName, item.PdfFieldType, item.FieldPurpose,
                item.BindingKey, item.FormatKey, item.IsUserEditable))
            .ToArrayAsync(cancellationToken);
        return MapMappingSet(template, version, mappings);
    }

    public async Task<FormTemplateMappingWriteResult> ReplaceMappingsAsync(
        Guid templateId,
        Guid versionId,
        byte[] expectedRowVersion,
        IReadOnlyList<FormTemplateMappingUpdate> mappings,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var template = await dbContext.FormTemplates
            .SingleOrDefaultAsync(item => item.Id == templateId, cancellationToken);
        if (template is null || template.OwnerType != FormTemplateOwnerType.Platform)
            return new(FormTemplateMappingWriteStatus.NotFound, null);
        var version = await dbContext.FormTemplateVersions
            .SingleOrDefaultAsync(item => item.FormTemplateId == templateId && item.Id == versionId,
                cancellationToken);
        if (version is null) return new(FormTemplateMappingWriteStatus.NotFound, null);
        if (version.Status != FormTemplateVersionStatus.Draft)
            return new(FormTemplateMappingWriteStatus.InvalidState, null);
        if (!version.RowVersion.SequenceEqual(expectedRowVersion))
            return new(FormTemplateMappingWriteStatus.Conflict, null);

        var entities = await dbContext.FormTemplateFieldMappings
            .Where(item => item.FormTemplateVersionId == versionId)
            .ToArrayAsync(cancellationToken);
        if (entities.Length != mappings.Count
            || entities.Select(item => item.Id).ToHashSet().SetEquals(mappings.Select(item => item.Id)) is false)
            return new(FormTemplateMappingWriteStatus.IncompleteSet, null);

        var updates = mappings.ToDictionary(item => item.Id);
        foreach (var entity in entities)
        {
            var update = updates[entity.Id];
            entity.Update(update.FieldPurpose, update.BindingKey, nowUtc);
        }
        version.MarkMappingsReviewed(actorUserId, nowUtc);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(FormTemplateMappingWriteStatus.Conflict, null);
        }

        return new(FormTemplateMappingWriteStatus.Succeeded,
            MapMappingSet(template, version, entities
                .OrderBy(item => item.PdfFieldName)
                .Select(item => new FormTemplateFieldDetails(
                    item.Id, item.PdfFieldName, item.PdfFieldType, item.FieldPurpose,
                    item.BindingKey, item.FormatKey, item.IsUserEditable)).ToArray()));
    }

    public async Task<FormTemplateMappingWriteResult> PublishAsync(
        Guid templateId,
        Guid versionId,
        byte[] expectedRowVersion,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var template = await dbContext.FormTemplates
            .SingleOrDefaultAsync(item => item.Id == templateId, cancellationToken);
        if (template is null || template.OwnerType != FormTemplateOwnerType.Platform)
            return new(FormTemplateMappingWriteStatus.NotFound, null);
        var version = await dbContext.FormTemplateVersions
            .SingleOrDefaultAsync(item => item.FormTemplateId == templateId && item.Id == versionId,
                cancellationToken);
        if (version is null) return new(FormTemplateMappingWriteStatus.NotFound, null);
        if (version.Status != FormTemplateVersionStatus.Draft)
            return new(FormTemplateMappingWriteStatus.InvalidState, null);
        if (!version.RowVersion.SequenceEqual(expectedRowVersion))
            return new(FormTemplateMappingWriteStatus.Conflict, null);

        var previous = await dbContext.FormTemplateVersions
            .Where(item => item.FormTemplateId == templateId
                && item.Status == FormTemplateVersionStatus.Published
                && item.Id != versionId)
            .ToArrayAsync(cancellationToken);
        foreach (var published in previous) published.Retire();
        version.Publish(actorUserId, nowUtc);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(FormTemplateMappingWriteStatus.Conflict, null);
        }

        var mappings = await dbContext.FormTemplateFieldMappings.AsNoTracking()
            .Where(item => item.FormTemplateVersionId == versionId)
            .OrderBy(item => item.PdfFieldName)
            .Select(item => new FormTemplateFieldDetails(
                item.Id, item.PdfFieldName, item.PdfFieldType, item.FieldPurpose,
                item.BindingKey, item.FormatKey, item.IsUserEditable))
            .ToArrayAsync(cancellationToken);
        return new(FormTemplateMappingWriteStatus.Succeeded, MapMappingSet(template, version, mappings));
    }

    private IQueryable<FormTemplate> VisibleTemplates(TenantScope scope) =>
        dbContext.FormTemplates.AsNoTracking().Where(template =>
            scope.IsPlatformAdministrator
            || template.OwnerType == FormTemplateOwnerType.Platform
            || (template.OwnerType == FormTemplateOwnerType.Company && template.CompanyId == scope.CompanyId));

    private static FormTemplateVersionDetails MapVersion(FormTemplateVersion version, int fieldCount) =>
        new(version.Id, version.FormTemplateId, version.VersionNumber, version.Status,
            version.OriginalFileName, version.StorageObjectKey, version.ContentType,
            version.SizeBytes, version.Sha256, version.PageCount, version.FormTechnology,
            version.EffectiveFrom, version.EffectiveTo, version.UploadedAtUtc, fieldCount,
            version.RequiresWarningAcknowledgement, version.MappingsReviewedByUserId,
            version.MappingsReviewedAtUtc, version.PublishedByUserId, version.PublishedAtUtc,
            version.RowVersion);

    private static FormTemplateMappingSet MapMappingSet(
        FormTemplate template,
        FormTemplateVersion version,
        IReadOnlyList<FormTemplateFieldDetails> mappings) => new(
            template.Id, template.Key, version.Id, version.VersionNumber, version.Status,
            version.RequiresWarningAcknowledgement, version.MappingsReviewedByUserId,
            version.MappingsReviewedAtUtc, version.RowVersion, mappings);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
