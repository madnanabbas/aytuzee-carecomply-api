using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Clients;

internal sealed class ClientBasicDetailsPersistence(
    CareComplyDbContext context,
    IClientSensitiveData sensitiveData,
    ClientAssignmentResolver assignmentResolver,
    TimeProvider timeProvider)
{
    public async Task<ClientBasicDetailsPreparation> PrepareAsync(
        Guid companyId,
        ClientBasicDetailsCreateRequest request,
        CancellationToken cancellationToken)
    {
        var location = await assignmentResolver.ResolveLocationAsync(
            companyId,
            request.CareSetup.LocationId,
            request.CareSetup.Location,
            cancellationToken);
        if (location is null)
        {
            return ClientBasicDetailsPreparation.Failed(ClientAdministrationStatus.InvalidState);
        }

        if (!await assignmentResolver.IsValidSupervisorAsync(
                request.CareSetup.SupervisorUserId,
                companyId,
                location.Id,
                cancellationToken))
        {
            return ClientBasicDetailsPreparation.Failed(ClientAdministrationStatus.InvalidState);
        }

        var pmiNumber = request.Identity.PmiNumber.Trim();
        if (await context.Clients.AsNoTracking().AnyAsync(
                client => client.CompanyId == companyId && client.PmiNumber == pmiNumber,
                cancellationToken))
        {
            return ClientBasicDetailsPreparation.Failed(ClientAdministrationStatus.DuplicatePmi);
        }

        var now = timeProvider.GetUtcNow();
        var clientId = Guid.CreateVersion7();
        var client = Client.Create(
            clientId,
            companyId,
            request.Identity.FirstName,
            request.Identity.MiddleName,
            request.Identity.LastName,
            request.Identity.PreferredName,
            request.Identity.DateOfBirth,
            request.Identity.Gender,
            pmiNumber,
            request.CareSetup.SocDate,
            request.CareSetup.IntakeDate,
            ClientStatus.Pending,
            now);

        if (!string.IsNullOrWhiteSpace(request.Identity.Ssn))
        {
            client.ChangeProtectedSsn(sensitiveData.Protect(request.Identity.Ssn), now);
        }

        client.ChangeCareSetup(
            request.CareSetup.IntakeDate,
            request.CareSetup.SocDate,
            ClientStatus.Pending,
            request.CareSetup.ReferralSource,
            location.Name,
            request.InsuranceProvider,
            request.CareSetup.IsOnboarding,
            request.CareSetup.EvvAuthorizedHoursPerWeek,
            null,
            null,
            now);
        client.AssignLocation(location.Id, location.Name, now);
        client.AssignSupervisor(request.CareSetup.SupervisorUserId, now);
        client.AdvanceCreationProgress(2, now);

        var services = request.Services.Select(service => new ClientService(
            Guid.CreateVersion7(),
            clientId,
            service.Code,
            service.Name,
            request.CareSetup.SocDate)).ToArray();
        context.Clients.Add(client);
        context.ClientServices.AddRange(services);
        return ClientBasicDetailsPreparation.Succeeded(client);
    }
}

internal sealed record ClientBasicDetailsPreparation(
    ClientAdministrationStatus Status,
    Client? Client)
{
    public static ClientBasicDetailsPreparation Succeeded(Client client) =>
        new(ClientAdministrationStatus.Succeeded, client);

    public static ClientBasicDetailsPreparation Failed(ClientAdministrationStatus status) =>
        new(status, null);
}
