using CareComply.Domain.Clients;

namespace CareComply.Domain.Tests;

public sealed class ClientResponsiblePartyTests
{
    [Fact]
    public void UpdatingResponsiblePartyPreservesTheClientAndContactType()
    {
        var clientId = Guid.NewGuid();
        var party = new ClientContact(Guid.NewGuid(), clientId, ClientContactType.ResponsibleParty,
            "Alex Smith", "Parent", "(612) 555-0111", null, false, false, null, null, null);

        party.UpdateResponsibleParty(" Alex Smith ", " Guardian ", "(612) 555-0123",
            "alex@example.test", "123 Main St", true, false, true);

        Assert.Equal(clientId, party.ClientId);
        Assert.Equal(ClientContactType.ResponsibleParty, party.Type);
        Assert.Equal("Guardian", party.Relationship);
        Assert.Equal("123 Main St", party.Address);
        Assert.True(party.LivesWithClient);
        Assert.False(party.HasSpenddown);
        Assert.True(party.IsActive);
    }

    [Fact]
    public void OtherContactTypesCannotBeChangedThroughResponsiblePartyFlow()
    {
        var contact = new ClientContact(Guid.NewGuid(), Guid.NewGuid(), ClientContactType.Emergency,
            "Alex", null, null, null, false, true, null, null, null);

        Assert.Throws<InvalidOperationException>(() => contact.UpdateResponsibleParty(
            "Alex", "Parent", "(612) 555-0111", null, null, false, false, true));
    }
}
