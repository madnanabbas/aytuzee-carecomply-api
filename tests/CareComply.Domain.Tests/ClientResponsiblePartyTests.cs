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

    [Fact]
    public void StructuredNamesAreOptionalIndependentValuesAndAreNeverParsedFromName()
    {
        var party = new ClientContact(Guid.NewGuid(), Guid.NewGuid(), ClientContactType.ResponsibleParty,
            "Morgan Rivera", "Parent", "(612) 555-0111", null, true, false, null, null, null);

        party.UpdateResponsibleParty(
            "Morgan Rivera", "Parent", "(612) 555-0111", null, null, false, false, true);

        Assert.Null(party.FirstName);
        Assert.Null(party.MiddleName);
        Assert.Null(party.LastName);

        party.UpdateResponsibleParty(
            "Morgan Rivera", "Parent", "(612) 555-0111", null, null, false, false, true,
            " Morgan ", " Lee ", " Rivera ");

        Assert.Equal("Morgan", party.FirstName);
        Assert.Equal("Lee", party.MiddleName);
        Assert.Equal("Rivera", party.LastName);
        Assert.Equal("Morgan Rivera", party.Name);
    }
}
