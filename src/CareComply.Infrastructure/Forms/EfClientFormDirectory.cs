using System.Globalization;
using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Application.Features.ClientForms;
using CareComply.Domain.Clients;
using CareComply.Domain.Forms;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Forms;

internal sealed class EfClientFormDirectory(
    CareComplyDbContext context,
    ITenantScopeAccessor tenantScopeAccessor,
    TimeProvider timeProvider) : IClientFormDirectory
{
    public async Task<IReadOnlyList<ClientFormTemplateSummary>?> ListAvailableAsync(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var client = await GetAccessibleClientAsync(clientId, cancellationToken);
        if (client is null) return null;

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return await (from version in context.FormTemplateVersions.AsNoTracking()
                join template in context.FormTemplates.AsNoTracking()
                    on version.FormTemplateId equals template.Id
                where template.IsActive
                    && version.Status == FormTemplateVersionStatus.Published
                    && (version.EffectiveFrom == null || version.EffectiveFrom <= today)
                    && (version.EffectiveTo == null || version.EffectiveTo >= today)
                    && (template.OwnerType == FormTemplateOwnerType.Platform
                        || (template.OwnerType == FormTemplateOwnerType.Company
                            && template.CompanyId == client.CompanyId))
                orderby template.Name, version.VersionNumber descending
                select new ClientFormTemplateSummary(
                    version.Id,
                    template.Key,
                    template.Name,
                    template.Description,
                    template.Category,
                    version.VersionNumber,
                    version.PageCount,
                    version.EffectiveFrom,
                    version.EffectiveTo))
            .ToListAsync(cancellationToken);
    }

    public async Task<ClientFormContentDescriptor?> GetContentDescriptorAsync(
        Guid clientId,
        Guid templateVersionId,
        CancellationToken cancellationToken)
    {
        var client = await GetAccessibleClientAsync(clientId, cancellationToken);
        return client is null
            ? null
            : await GetVisibleVersionAsync(templateVersionId, client.CompanyId, cancellationToken);
    }

    public async Task<ClientFormBindings?> GetBindingsAsync(
        Guid clientId,
        Guid templateVersionId,
        CancellationToken cancellationToken)
    {
        var client = await GetAccessibleClientAsync(clientId, cancellationToken);
        if (client is null) return null;

        var version = await GetVisibleVersionAsync(
            templateVersionId,
            client.CompanyId,
            cancellationToken);
        if (version is null) return null;

        var responsibleParty = await context.ClientContacts.AsNoTracking()
            .Where(contact => contact.ClientId == clientId
                && contact.Type == ClientContactType.ResponsibleParty
                && contact.IsActive)
            .OrderByDescending(contact => contact.IsPrimary)
            .ThenBy(contact => contact.Name)
            .ThenBy(contact => contact.Id)
            .Select(contact => new ResponsiblePartyData(
                contact.Name,
                contact.FirstName,
                contact.MiddleName,
                contact.LastName,
                contact.Relationship,
                contact.Phone,
                contact.Email,
                contact.Address,
                contact.City,
                contact.State,
                contact.PostalCode))
            .FirstOrDefaultAsync(cancellationToken);

        var mappings = await context.FormTemplateFieldMappings.AsNoTracking()
            .Where(mapping => mapping.FormTemplateVersionId == templateVersionId
                && mapping.FieldPurpose == FormFieldPurpose.Data)
            .OrderBy(mapping => mapping.PdfFieldName)
            .Select(mapping => new
            {
                mapping.PdfFieldName,
                mapping.BindingKey,
                mapping.IsUserEditable,
            })
            .ToListAsync(cancellationToken);

        var fields = mappings
            .Where(mapping => ClientFormBindingKeys.IsAllowed(mapping.BindingKey))
            .Select(mapping => new ClientFormBindingField(
                mapping.PdfFieldName,
                Resolve(mapping.BindingKey!, client, responsibleParty),
                mapping.IsUserEditable))
            .ToArray();

        return new ClientFormBindings(
            version.TemplateVersionId,
            version.TemplateKey,
            version.VersionNumber,
            fields);
    }

    private async Task<ClientData?> GetAccessibleClientAsync(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null) return null;

        return await context.Clients.AsNoTracking()
            .Where(client => client.Id == clientId
                && (tenant.IsPlatformAdministrator
                    || (client.CompanyId == tenant.CompanyId
                        && context.UserLocations.Any(access =>
                            access.UserId == tenant.UserId
                            && access.LocationId == client.LocationId))))
            .Select(client => new ClientData(
                client.CompanyId,
                context.Companies.Where(company => company.Id == client.CompanyId)
                    .Select(company => company.Name).Single(),
                client.FirstName,
                client.MiddleName,
                client.LastName,
                client.PreferredName,
                client.DateOfBirth,
                client.PmiNumber,
                client.Email,
                client.PrimaryPhone,
                client.SecondaryPhone,
                client.AddressLine1,
                client.AddressLine2,
                client.City,
                client.State,
                client.PostalCode,
                client.County,
                client.Country,
                client.IntakeDate,
                client.SocDate,
                client.DischargeDate))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<ClientFormContentDescriptor?> GetVisibleVersionAsync(
        Guid templateVersionId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return await (from version in context.FormTemplateVersions.AsNoTracking()
                join template in context.FormTemplates.AsNoTracking()
                    on version.FormTemplateId equals template.Id
                where version.Id == templateVersionId
                    && template.IsActive
                    && version.Status == FormTemplateVersionStatus.Published
                    && (version.EffectiveFrom == null || version.EffectiveFrom <= today)
                    && (version.EffectiveTo == null || version.EffectiveTo >= today)
                    && (template.OwnerType == FormTemplateOwnerType.Platform
                        || (template.OwnerType == FormTemplateOwnerType.Company
                            && template.CompanyId == companyId))
                select new ClientFormContentDescriptor(
                    version.Id,
                    template.Key,
                    version.VersionNumber,
                    version.StorageObjectKey,
                    version.ContentType))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static string Resolve(
        string key,
        ClientData client,
        ResponsiblePartyData? party) => key switch
    {
        ClientFormBindingKeys.ClientFirstName => client.FirstName,
        ClientFormBindingKeys.ClientMiddleName => client.MiddleName,
        ClientFormBindingKeys.ClientMiddleInitial => Initial(client.MiddleName),
        ClientFormBindingKeys.ClientLastName => client.LastName,
        ClientFormBindingKeys.ClientPreferredName => client.PreferredName,
        ClientFormBindingKeys.ClientDateOfBirth => Date(client.DateOfBirth),
        ClientFormBindingKeys.ClientPmiNumber => client.PmiNumber,
        ClientFormBindingKeys.ClientEmail => client.Email,
        ClientFormBindingKeys.ClientPrimaryPhone => client.PrimaryPhone,
        ClientFormBindingKeys.ClientSecondaryPhone => client.SecondaryPhone,
        ClientFormBindingKeys.ClientAddressLine1 => client.AddressLine1,
        ClientFormBindingKeys.ClientAddressLine2 => client.AddressLine2,
        ClientFormBindingKeys.ClientCity => client.City,
        ClientFormBindingKeys.ClientState => client.State,
        ClientFormBindingKeys.ClientPostalCode => client.PostalCode,
        ClientFormBindingKeys.ClientCounty => client.County,
        ClientFormBindingKeys.ClientCountry => client.Country,
        ClientFormBindingKeys.ClientIntakeDate => Date(client.IntakeDate),
        ClientFormBindingKeys.ClientSocDate => Date(client.SocDate),
        ClientFormBindingKeys.ClientDischargeDate => Date(client.DischargeDate),
        ClientFormBindingKeys.ResponsiblePartyFullName => party?.Name ?? string.Empty,
        ClientFormBindingKeys.ResponsiblePartyFirstName => party?.FirstName ?? string.Empty,
        ClientFormBindingKeys.ResponsiblePartyMiddleName => party?.MiddleName ?? string.Empty,
        ClientFormBindingKeys.ResponsiblePartyMiddleInitial => Initial(party?.MiddleName),
        ClientFormBindingKeys.ResponsiblePartyLastName => party?.LastName ?? string.Empty,
        ClientFormBindingKeys.ResponsiblePartyRelationship => party?.Relationship ?? string.Empty,
        ClientFormBindingKeys.ResponsiblePartyPhone => party?.Phone ?? string.Empty,
        ClientFormBindingKeys.ResponsiblePartyEmail => party?.Email ?? string.Empty,
        ClientFormBindingKeys.ResponsiblePartyAddress => party?.Address ?? string.Empty,
        ClientFormBindingKeys.ResponsiblePartyCity => party?.City ?? string.Empty,
        ClientFormBindingKeys.ResponsiblePartyState => party?.State ?? string.Empty,
        ClientFormBindingKeys.ResponsiblePartyPostalCode => party?.PostalCode ?? string.Empty,
        ClientFormBindingKeys.CompanyName => client.CompanyName,
        _ => string.Empty,
    };

    private static string Date(DateOnly? value) =>
        value?.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Initial(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim()[..1];

    private sealed record ClientData(
        Guid CompanyId,
        string CompanyName,
        string FirstName,
        string MiddleName,
        string LastName,
        string PreferredName,
        DateOnly DateOfBirth,
        string PmiNumber,
        string Email,
        string PrimaryPhone,
        string SecondaryPhone,
        string AddressLine1,
        string AddressLine2,
        string City,
        string State,
        string PostalCode,
        string County,
        string Country,
        DateOnly? IntakeDate,
        DateOnly SocDate,
        DateOnly? DischargeDate);

    private sealed record ResponsiblePartyData(
        string Name,
        string? FirstName,
        string? MiddleName,
        string? LastName,
        string Relationship,
        string Phone,
        string Email,
        string Address,
        string City,
        string State,
        string PostalCode);
}
