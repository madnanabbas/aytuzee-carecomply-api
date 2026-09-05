using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientCaregivers;
using CareComply.Domain.Clients;

namespace CareComply.Application.Tests;

public sealed class ClientCaregiverApplicationTests
{
    [Fact]
    public void CommandsUseCanonicalPermissions()
    {
        var client = Guid.NewGuid(); var assignment = Guid.NewGuid(); var version = new string('A', 64);
        Assert.Equal(Permissions.ClientCaregiver.View, new ListClientCaregiversQuery(client).RequiredPermission);
        Assert.Equal(Permissions.ClientCaregiver.Create, new CreateClientCaregiverCommand(client, Guid.NewGuid(), ClientCaregiverType.Primary, true, null, new(2026, 9, 4), null, null).RequiredPermission);
        Assert.Equal(Permissions.ClientCaregiver.Edit, new UpdateClientCaregiverCommand(client, assignment, ClientCaregiverType.Backup, false, null, new(2026, 9, 4), null, null, version).RequiredPermission);
        Assert.Equal(Permissions.ClientCaregiver.Edit, new EndClientCaregiverCommand(client, assignment, new(2026, 9, 5), version).RequiredPermission);
    }

    [Fact]
    public void CreateValidatorRejectsInvalidDateRange()
    {
        var command = new CreateClientCaregiverCommand(Guid.NewGuid(), Guid.NewGuid(), ClientCaregiverType.Primary,
            true, null, new(2026, 9, 5), new(2026, 9, 4), null);
        var result = new CreateClientCaregiverValidator().Validate(command);
        Assert.False(result.IsValid);
    }
}
