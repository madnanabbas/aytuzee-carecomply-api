using CareComply.Domain.Clients;

namespace CareComply.Domain.Tests;

public sealed class ClientContactsTests
{
    [Theory]
    [InlineData(ClientContactType.General)]
    [InlineData(ClientContactType.Emergency)]
    public void UpdatingContactPreservesItsTypeAndClient(ClientContactType type)
    {
        var clientId = Guid.NewGuid();
        var contact = new ClientContact(Guid.NewGuid(), clientId, type, "Pat Smith", "Friend",
            "(612) 555-0100", null, false, type == ClientContactType.Emergency, null, null, null);

        contact.UpdateContactDetails(" Pat Smith ", " Neighbor ", "(612) 555-0101",
            "pat@example.test", "45 Lake Street", false);

        Assert.Equal(clientId, contact.ClientId);
        Assert.Equal(type, contact.Type);
        Assert.Equal("Neighbor", contact.Relationship);
        Assert.Equal("45 Lake Street", contact.Address);
        Assert.False(contact.IsActive);
    }

    [Fact]
    public void ResponsiblePartyCannotBeModifiedThroughContactsForm()
    {
        var contact = new ClientContact(Guid.NewGuid(), Guid.NewGuid(), ClientContactType.ResponsibleParty,
            "Pat", "Parent", "(612) 555-0100", null, false, false, null, null, null);

        Assert.Throws<InvalidOperationException>(() => contact.UpdateContactDetails(
            "Pat", "Friend", "(612) 555-0101", null, null, true));
    }
}
