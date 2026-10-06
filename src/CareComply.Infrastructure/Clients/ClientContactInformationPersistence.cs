using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Clients;

internal sealed class ClientContactInformationPersistence(
    CareComplyDbContext context,
    ClientLanguageSynchronizer languageSynchronizer)
{
    public async Task<ClientContactInformationPersistenceResult> ApplyAsync(
        Client client,
        ClientContactInput contact,
        IReadOnlyList<ClientLanguageInput> requestedLanguages,
        string editorVersion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is not null)
            return await ApplyCoreAsync(
                client, contact, requestedLanguages, editorVersion, now, cancellationToken);

        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var result = await ApplyCoreAsync(
                client, contact, requestedLanguages, editorVersion, now, cancellationToken);
            if (result.Status is ClientAdministrationStatus.Succeeded or ClientAdministrationStatus.NoChanges)
                await transaction.CommitAsync(cancellationToken);
            else
                await transaction.RollbackAsync(cancellationToken);
            return result;
        });
    }

    private async Task<ClientContactInformationPersistenceResult> ApplyCoreAsync(
        Client client,
        ClientContactInput contact,
        IReadOnlyList<ClientLanguageInput> requestedLanguages,
        string editorVersion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!ClientEditorVersion.Matches(client.RowVersion, editorVersion))
            return new(ClientAdministrationStatus.Conflict, false, false);
        var languages = await context.ClientLanguagePreferences
            .Where(item => item.ClientId == client.Id).ToListAsync(cancellationToken);
        var contactChanged = ClientChangeDetector.ContactChanged(client, contact);
        var languagesChanged = ClientLanguageSynchronizer.HasChanges(languages, requestedLanguages);
        bool progressChanged;
        try { progressChanged = client.AdvanceCreationProgress(4, now); }
        catch (InvalidOperationException) { return new(ClientAdministrationStatus.InvalidState, false, false); }
        if (!contactChanged && !languagesChanged && !progressChanged)
            return new(ClientAdministrationStatus.NoChanges, false, false);

        try
        {
            if (contactChanged)
                client.ChangeContact(contact.Email, contact.PrimaryPhone, contact.SecondaryPhone,
                    contact.AddressLine1, contact.AddressLine2, contact.City, contact.State,
                    contact.PostalCode, contact.County, contact.Country, now);
            if (languagesChanged)
                await languageSynchronizer.ReplaceAsync(client.Id, languages, requestedLanguages,
                    client, now, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return new(ClientAdministrationStatus.Succeeded, contactChanged, languagesChanged);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(ClientAdministrationStatus.Conflict, false, false);
        }
        catch (ArgumentException)
        {
            return new(ClientAdministrationStatus.InvalidState, false, false);
        }
    }
}

internal sealed record ClientContactInformationPersistenceResult(
    ClientAdministrationStatus Status,
    bool ContactChanged,
    bool LanguagesChanged);
