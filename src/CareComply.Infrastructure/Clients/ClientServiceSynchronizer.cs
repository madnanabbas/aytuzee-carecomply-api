using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;

namespace CareComply.Infrastructure.Clients;

internal sealed class ClientServiceSynchronizer(CareComplyDbContext context)
{
    public static ClientServiceChangeAnalysis Analyze(
        IReadOnlyList<ClientService> activeServices,
        IReadOnlyList<ClientServiceInput> requestedServices)
    {
        var currentByCode = activeServices.ToDictionary(
            service => service.Code,
            StringComparer.OrdinalIgnoreCase);
        var requestedByCode = requestedServices.ToDictionary(
            service => service.Code.Trim(),
            StringComparer.OrdinalIgnoreCase);

        var toEnd = activeServices
            .Where(service => !requestedByCode.ContainsKey(service.Code))
            .ToArray();
        var toAdd = requestedServices
            .Where(service => !currentByCode.ContainsKey(service.Code.Trim()))
            .ToArray();
        var toRename = activeServices
            .Where(service => requestedByCode.TryGetValue(service.Code, out var requested)
                && !ClientChangeDetector.Equal(service.Name, requested.Name))
            .ToArray();

        return new(
            toEnd,
            toAdd,
            toRename,
            toEnd.Length > 0 || toAdd.Length > 0,
            toEnd.Length > 0 || toAdd.Length > 0 || toRename.Length > 0);
    }

    public void Apply(
        Guid clientId,
        IReadOnlyList<ClientServiceInput> requestedServices,
        ClientServiceChangeAnalysis analysis,
        DateOnly? effectiveDate,
        DateTimeOffset now,
        Client client)
    {
        var requestedByCode = requestedServices.ToDictionary(
            service => service.Code.Trim(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var service in analysis.ServicesToRename)
        {
            service.Rename(requestedByCode[service.Code].Name);
        }

        if (analysis.ActiveSetChanged)
        {
            var changeDate = effectiveDate
                ?? throw new InvalidOperationException(
                    "A service change date is required for an active service set change.");

            foreach (var service in analysis.ServicesToEnd)
            {
                service.End(changeDate);
            }

            foreach (var service in analysis.ServicesToAdd)
            {
                context.ClientServices.Add(new ClientService(
                    Guid.CreateVersion7(),
                    clientId,
                    service.Code,
                    service.Name,
                    changeDate));
            }
        }

        client.MarkUpdated(now);
    }
}

internal sealed record ClientServiceChangeAnalysis(
    IReadOnlyList<ClientService> ServicesToEnd,
    IReadOnlyList<ClientServiceInput> ServicesToAdd,
    IReadOnlyList<ClientService> ServicesToRename,
    bool ActiveSetChanged,
    bool HasChanges);
