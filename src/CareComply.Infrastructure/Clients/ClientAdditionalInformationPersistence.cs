using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Clients;

internal static class ClientAdditionalInformationPersistence
{
    public static async Task<ClientAdministrationStatus> ApplyAsync(
        CareComplyDbContext context,
        Client client,
        ClientAdditionalProfileInput input,
        string editorVersion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!ClientEditorVersion.Matches(client.RowVersion, editorVersion))
        {
            return ClientAdministrationStatus.Conflict;
        }

        var profile = await context.ClientAdditionalProfiles.SingleOrDefaultAsync(
            item => item.ClientId == client.Id,
            cancellationToken);
        bool progressChanged;
        try
        {
            progressChanged = client.AdvanceCreationProgress(3, now);
        }
        catch (InvalidOperationException)
        {
            return ClientAdministrationStatus.InvalidState;
        }

        if (!ClientProfilePersistence.HasChanges(profile, input) && !progressChanged)
        {
            return ClientAdministrationStatus.NoChanges;
        }

        if (profile is null)
        {
            profile = new ClientAdditionalProfile(client.Id);
            context.ClientAdditionalProfiles.Add(profile);
        }

        ClientProfilePersistence.Apply(profile, input);
        if (!progressChanged)
        {
            client.MarkUpdated(now);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return ClientAdministrationStatus.Succeeded;
        }
        catch (DbUpdateConcurrencyException)
        {
            return ClientAdministrationStatus.Conflict;
        }
        catch (ArgumentException)
        {
            return ClientAdministrationStatus.InvalidState;
        }
    }
}
