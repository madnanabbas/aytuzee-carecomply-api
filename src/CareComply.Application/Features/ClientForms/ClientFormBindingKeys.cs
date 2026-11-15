using CareComply.Domain.Forms;

namespace CareComply.Application.Features.ClientForms;

public sealed record ClientFormBindingDefinition(
    string Key,
    string Label,
    string Group,
    IReadOnlySet<FormFieldType> AllowedPdfFieldTypes,
    string SyntheticValue);

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

    private static readonly IReadOnlySet<FormFieldType> TextFieldTypes =
        new HashSet<FormFieldType> { FormFieldType.Text, FormFieldType.Choice };

    public static IReadOnlyList<ClientFormBindingDefinition> Catalog { get; } =
    [
        Definition(ClientFirstName, "First name", "Client", "Avery"),
        Definition(ClientMiddleName, "Middle name", "Client", "Jordan"),
        Definition(ClientMiddleInitial, "Middle initial", "Client", "J"),
        Definition(ClientLastName, "Last name", "Client", "Morgan"),
        Definition(ClientPreferredName, "Preferred name", "Client", "Avery"),
        Definition(ClientDateOfBirth, "Date of birth", "Client", "01/15/1990"),
        Definition(ClientPmiNumber, "PMI number", "Client", "TEST-10001"),
        Definition(ClientEmail, "Email", "Client", "avery.morgan@example.test"),
        Definition(ClientPrimaryPhone, "Primary phone", "Client", "(612) 555-0101"),
        Definition(ClientSecondaryPhone, "Secondary phone", "Client", "(612) 555-0102"),
        Definition(ClientAddressLine1, "Address line 1", "Client", "123 Test Avenue"),
        Definition(ClientAddressLine2, "Address line 2", "Client", "Unit 4"),
        Definition(ClientCity, "City", "Client", "Minneapolis"),
        Definition(ClientState, "State", "Client", "MN"),
        Definition(ClientPostalCode, "Postal code", "Client", "55401"),
        Definition(ClientCounty, "County", "Client", "Hennepin"),
        Definition(ClientCountry, "Country", "Client", "United States"),
        Definition(ClientIntakeDate, "Intake date", "Client", "10/01/2026"),
        Definition(ClientSocDate, "Start of care date", "Client", "10/15/2026"),
        Definition(ClientDischargeDate, "Discharge date", "Client", ""),
        Definition(ResponsiblePartyFullName, "Full name", "Responsible Party", "Riley Morgan"),
        Definition(ResponsiblePartyFirstName, "First name", "Responsible Party", "Riley"),
        Definition(ResponsiblePartyMiddleName, "Middle name", "Responsible Party", "Casey"),
        Definition(ResponsiblePartyMiddleInitial, "Middle initial", "Responsible Party", "C"),
        Definition(ResponsiblePartyLastName, "Last name", "Responsible Party", "Morgan"),
        Definition(ResponsiblePartyRelationship, "Relationship", "Responsible Party", "Parent"),
        Definition(ResponsiblePartyPhone, "Phone", "Responsible Party", "(612) 555-0110"),
        Definition(ResponsiblePartyEmail, "Email", "Responsible Party", "riley.morgan@example.test"),
        Definition(ResponsiblePartyAddress, "Address", "Responsible Party", "456 Example Street"),
        Definition(ResponsiblePartyCity, "City", "Responsible Party", "Saint Paul"),
        Definition(ResponsiblePartyState, "State", "Responsible Party", "MN"),
        Definition(ResponsiblePartyPostalCode, "Postal code", "Responsible Party", "55101"),
        Definition(CompanyName, "Company name", "Company", "Synthetic Care Provider"),
    ];

    public static IReadOnlySet<string> All { get; } =
        Catalog.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);

    public static ClientFormBindingDefinition? Find(string? key) =>
        key is null ? null : Catalog.FirstOrDefault(item => item.Key == key);

    public static bool IsAllowed(string? key) => key is not null && All.Contains(key);

    private static ClientFormBindingDefinition Definition(
        string key,
        string label,
        string group,
        string syntheticValue) => new(key, label, group, TextFieldTypes, syntheticValue);
}
