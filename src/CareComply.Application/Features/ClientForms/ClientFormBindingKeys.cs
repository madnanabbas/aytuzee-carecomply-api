namespace CareComply.Application.Features.ClientForms;

public static class ClientFormBindingKeys
{
    public const string ClientFirstName = "client.firstName";
    public const string ClientMiddleName = "client.middleName";
    public const string ClientMiddleInitial = "client.middleInitial";
    public const string ClientLastName = "client.lastName";
    public const string ClientPreferredName = "client.preferredName";
    public const string ClientDateOfBirth = "client.dateOfBirth";
    public const string ClientPmiNumber = "client.pmiNumber";
    public const string ClientEmail = "client.email";
    public const string ClientPrimaryPhone = "client.primaryPhone";
    public const string ClientSecondaryPhone = "client.secondaryPhone";
    public const string ClientAddressLine1 = "client.addressLine1";
    public const string ClientAddressLine2 = "client.addressLine2";
    public const string ClientCity = "client.city";
    public const string ClientState = "client.state";
    public const string ClientPostalCode = "client.postalCode";
    public const string ClientCounty = "client.county";
    public const string ClientCountry = "client.country";
    public const string ClientIntakeDate = "client.intakeDate";
    public const string ClientSocDate = "client.socDate";
    public const string ClientDischargeDate = "client.dischargeDate";
    public const string ResponsiblePartyFullName = "client.responsibleParty.fullName";
    public const string ResponsiblePartyFirstName = "client.responsibleParty.firstName";
    public const string ResponsiblePartyMiddleName = "client.responsibleParty.middleName";
    public const string ResponsiblePartyMiddleInitial = "client.responsibleParty.middleInitial";
    public const string ResponsiblePartyLastName = "client.responsibleParty.lastName";
    public const string ResponsiblePartyRelationship = "client.responsibleParty.relationship";
    public const string ResponsiblePartyPhone = "client.responsibleParty.phone";
    public const string ResponsiblePartyEmail = "client.responsibleParty.email";
    public const string ResponsiblePartyAddress = "client.responsibleParty.address";
    public const string ResponsiblePartyCity = "client.responsibleParty.city";
    public const string ResponsiblePartyState = "client.responsibleParty.state";
    public const string ResponsiblePartyPostalCode = "client.responsibleParty.postalCode";
    public const string CompanyName = "company.name";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ClientFirstName,
        ClientMiddleName,
        ClientMiddleInitial,
        ClientLastName,
        ClientPreferredName,
        ClientDateOfBirth,
        ClientPmiNumber,
        ClientEmail,
        ClientPrimaryPhone,
        ClientSecondaryPhone,
        ClientAddressLine1,
        ClientAddressLine2,
        ClientCity,
        ClientState,
        ClientPostalCode,
        ClientCounty,
        ClientCountry,
        ClientIntakeDate,
        ClientSocDate,
        ClientDischargeDate,
        ResponsiblePartyFullName,
        ResponsiblePartyFirstName,
        ResponsiblePartyMiddleName,
        ResponsiblePartyMiddleInitial,
        ResponsiblePartyLastName,
        ResponsiblePartyRelationship,
        ResponsiblePartyPhone,
        ResponsiblePartyEmail,
        ResponsiblePartyAddress,
        ResponsiblePartyCity,
        ResponsiblePartyState,
        ResponsiblePartyPostalCode,
        CompanyName,
    };

    public static bool IsAllowed(string? key) => key is not null && All.Contains(key);
}
