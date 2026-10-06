using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;

namespace CareComply.Infrastructure.Clients;

internal sealed class ClientLanguageSynchronizer(CareComplyDbContext context)
{
    public static bool HasChanges(
        IReadOnlyList<ClientLanguagePreference> current,
        IReadOnlyList<ClientLanguageInput> requested)
    {
        if (current.Count != requested.Count)
        {
            return true;
        }

        var requestedByCode = requested.ToDictionary(
            language => language.LanguageCode.Trim(),
            StringComparer.OrdinalIgnoreCase);
        foreach (var language in current)
        {
            if (!requestedByCode.TryGetValue(language.LanguageCode, out var desired)
                || !ClientChangeDetector.Equal(language.LanguageName, desired.LanguageName)
                || language.IsPrimary != desired.IsPrimary)
            {
                return true;
            }
        }

        return false;
    }

    public async Task ReplaceAsync(
        Guid clientId,
        IReadOnlyList<ClientLanguagePreference> current,
        IReadOnlyList<ClientLanguageInput> requested,
        Client client,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Preserve the separate delete statement: the filtered unique index must not observe
        // two primary language rows while preferences are being replaced.
        if (current.Count > 0)
        {
            context.ClientLanguagePreferences.RemoveRange(current);
            client.MarkUpdated(now);
            await context.SaveChangesAsync(cancellationToken);
        }

        context.ClientLanguagePreferences.AddRange(requested.Select(language =>
            new ClientLanguagePreference(
                clientId,
                language.LanguageCode,
                language.LanguageName,
                language.IsPrimary)));
        client.MarkUpdated(now);
    }
}
