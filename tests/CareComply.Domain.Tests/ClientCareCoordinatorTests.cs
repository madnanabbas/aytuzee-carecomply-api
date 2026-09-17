using CareComply.Domain.Clients;

namespace CareComply.Domain.Tests;

public sealed class ClientCareCoordinatorTests
{
    [Fact]
    public void UpdatesCoordinatorFieldsAndPreservesContactType()
    {
        var contact = new ClientContact(Guid.NewGuid(), Guid.NewGuid(), ClientContactType.CareCoordinator,
            "Taylor", null, null, null, false, false, null, null, null);

        contact.UpdateCareCoordinator(" Taylor Lee ", " Outside Agency ", "(612) 555-0101",
            " 234 ", "(612) 555-0102", "taylor@example.test", true);

        Assert.Equal(ClientContactType.CareCoordinator, contact.Type);
        Assert.Equal("Outside Agency", contact.CompanyName);
        Assert.Equal("234", contact.PhoneExtension);
        Assert.Equal("(612) 555-0102", contact.Fax);
        Assert.Equal("taylor@example.test", contact.Email);
    }

    [Fact]
    public void RejectsChangingOtherContactTypesThroughCareCoordinatorFlow()
    {
        var contact = new ClientContact(Guid.NewGuid(), Guid.NewGuid(), ClientContactType.ResponsibleParty,
            "Taylor", null, null, null, false, false, null, null, null);

        Assert.Throws<InvalidOperationException>(() => contact.UpdateCareCoordinator(
            "Taylor", null, null, null, null, null, true));
    }
}
