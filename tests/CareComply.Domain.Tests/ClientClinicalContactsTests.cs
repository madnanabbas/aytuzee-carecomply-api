using CareComply.Domain.Clients;

namespace CareComply.Domain.Tests;

public sealed class ClientClinicalContactsTests
{
    [Fact]
    public void ConsultationProviderKeepsItsSupportProviderDetails()
    {
        var contact = Contact(ClientContactType.ConsultationProvider);

        contact.UpdateConsultationProvider("Dr Lee", "(612) 555-0100", "lee@example.test",
            "Case worker", "(612) 555-0101", "10 Main Street", true);

        Assert.Equal("Case worker", contact.SecondaryName);
        Assert.Equal("10 Main Street", contact.SecondaryAddress);
        Assert.Equal("lee@example.test", contact.Email);
    }

    [Fact]
    public void PhysicianPreservesLeadingZeroInNpiAndRejectsLetters()
    {
        var contact = Contact(ClientContactType.Physician);

        contact.UpdatePhysician("Dr Lee", "0123456789", "(612) 555-0100", null,
            "12 Oak Ave", "55404", "Minneapolis", "Minnesota", true);

        Assert.Equal("0123456789", contact.Npi);
        Assert.Equal("Minneapolis", contact.City);
        Assert.Throws<ArgumentException>(() => contact.UpdatePhysician("Dr Lee", "01234abc89", null,
            null, null, null, null, null, true));
    }

    [Fact]
    public void ClinicalContactTypeCannotBeChangedThroughAnotherSection()
    {
        var contact = Contact(ClientContactType.ResponsibleParty);

        Assert.Throws<InvalidOperationException>(() => contact.UpdatePhysician("Dr Lee", null,
            null, null, null, null, null, null, true));
        Assert.Throws<InvalidOperationException>(() => contact.UpdateConsultationProvider("Dr Lee", null,
            null, null, null, null, true));
    }

    private static ClientContact Contact(ClientContactType type) => new(Guid.NewGuid(), Guid.NewGuid(),
        type, "Original name", null, null, null, false, false, null, null, null);
}
