using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientCareCoordinators;
using CareComply.Application.Features.ClientClinicalContacts;
using CareComply.Application.Features.ClientContacts;
using CareComply.Application.Features.ClientResponsibleParties;
using FluentValidation.Results;
using CareComply.Domain.Clients;

namespace CareComply.Application.Tests;

public sealed class ClientContactWorkflowApplicationTests
{
    [Fact]
    public void UsPhoneValidationUsesExactlyTenDigits()
    {
        var validator = new CreateClientContactValidator();
        var valid = validator.Validate(new CreateClientContactCommand(
            Guid.NewGuid(), "General", "Contact", "Sibling", "(612) 555-0100", null, null,
            true));
        var countryCode = validator.Validate(new CreateClientContactCommand(
            Guid.NewGuid(), "General", "Contact", "Sibling", "+1 (612) 555-0100", null, null,
            true));
        var incomplete = validator.Validate(new CreateClientContactCommand(
            Guid.NewGuid(), "General", "Contact", "Sibling", "612-555", null, null, true));
        var invalidCharacters = validator.Validate(new CreateClientContactCommand(
            Guid.NewGuid(), "General", "Contact", "Sibling", "abc6125550100", null, null,
            true));

        Assert.True(valid.IsValid);
        Assert.Contains(countryCode.Errors,
            error => error.PropertyName == "Phone"
                && error.ErrorMessage == "Phone must contain exactly 10 US digits.");
        Assert.Contains(incomplete.Errors,
            error => error.PropertyName == "Phone"
                && error.ErrorMessage == "Phone must contain exactly 10 US digits.");
        Assert.Contains(invalidCharacters.Errors,
            error => error.PropertyName == "Phone"
                && error.ErrorMessage == "Phone must contain exactly 10 US digits.");
    }

    [Fact]
    public void RequestsDeclareCanonicalPermissions()
    {
        var clientId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        Assert.Equal(Permissions.ClientCareCoordinator.View,
            new ListClientCareCoordinatorsQuery(clientId).RequiredPermission);
        Assert.Equal(Permissions.ClientCareCoordinator.Create,
            new CreateClientCareCoordinatorCommand(clientId, null, null, null, null, null, null, null)
                .RequiredPermission);
        Assert.Equal(Permissions.ClientCareCoordinator.Edit,
            new UpdateClientCareCoordinatorCommand(clientId, itemId, null, null, null, null, null,
                null, null).RequiredPermission);
        Assert.Equal(Permissions.ClientContact.View,
            new ListClientContactsQuery(clientId).RequiredPermission);
        Assert.Equal(Permissions.ClientContact.Create,
            new CreateClientContactCommand(clientId, null, null, null, null, null, null, null)
                .RequiredPermission);
        Assert.Equal(Permissions.ClientContact.Edit,
            new UpdateClientContactCommand(clientId, itemId, null, null, null, null, null, null,
                null).RequiredPermission);
        Assert.Equal(Permissions.ClientResponsibleParty.View,
            new ListClientResponsiblePartiesQuery(clientId).RequiredPermission);
        Assert.Equal(Permissions.ClientResponsibleParty.Create,
            new CreateClientResponsiblePartyCommand(clientId, null, null, null, null, null, null,
                null, null).RequiredPermission);
        Assert.Equal(Permissions.ClientResponsibleParty.Edit,
            new UpdateClientResponsiblePartyCommand(clientId, itemId, null, null, null, null, null,
                null, null, null).RequiredPermission);
        Assert.Equal(Permissions.ClientConsultationProvider.View,
            new ListConsultationProvidersQuery(clientId).RequiredPermission);
        Assert.Equal(Permissions.ClientConsultationProvider.Create,
            new CreateConsultationProviderCommand(clientId, null, null, null, null, null, null, null)
                .RequiredPermission);
        Assert.Equal(Permissions.ClientConsultationProvider.Edit,
            new UpdateConsultationProviderCommand(clientId, itemId, null, null, null, null, null,
                null, null).RequiredPermission);
        Assert.Equal(Permissions.ClientPhysician.View,
            new ListPhysiciansQuery(clientId).RequiredPermission);
        Assert.Equal(Permissions.ClientPhysician.Create,
            new CreatePhysicianCommand(clientId, null, null, null, null, null, null, null, null,
                null).RequiredPermission);
        Assert.Equal(Permissions.ClientPhysician.Edit,
            new UpdatePhysicianCommand(clientId, itemId, null, null, null, null, null, null, null,
                null, null).RequiredPermission);
    }

    [Fact]
    public void ValidatorsPreserveCharacterizedKeysAndMessages()
    {
        var clientId = Guid.NewGuid();

        AssertErrors(
            new CreateClientCareCoordinatorValidator().Validate(
                new CreateClientCareCoordinatorCommand(
                    clientId, null, null, null, null, null, null, null)),
            ("Name", "Name is required."),
            ("IsActive", "Select a status."));
        AssertErrors(
            new CreateClientContactValidator().Validate(
                new CreateClientContactCommand(
                    clientId, "Other", null, null, null, null, null, null)),
            ("Type", "Select General or Emergency."),
            ("Name", "Name is required."),
            ("Relationship", "Relationship is required."),
            ("Phone", "Phone is required."),
            ("IsActive", "Select a status."));
        AssertErrors(
            new CreateClientResponsiblePartyValidator().Validate(
                new CreateClientResponsiblePartyCommand(
                    clientId, null, null, null, null, null, null, null, null)),
            ("Name", "Name is required."),
            ("Relationship", "Relationship is required."),
            ("Phone", "Phone is required."),
            ("LivesWithClient", "Select whether this person lives with the client."),
            ("HasSpenddown", "Select whether this person has spenddown responsibility."),
            ("IsActive", "Select a status."));
        AssertErrors(
            new CreateConsultationProviderValidator().Validate(
                new CreateConsultationProviderCommand(
                    clientId, null, null, "invalid", null, null, null, null)),
            ("Name", "Name is required."),
            ("Email", "Enter a valid email address."),
            ("IsActive", "Select a status."));
        AssertErrors(
            new CreatePhysicianValidator().Validate(
                new CreatePhysicianCommand(
                    clientId, null, "abc", null, null, null, null, null, null, null)),
            ("Name", "Name is required."),
            ("Npi", "NPI must contain exactly 10 digits."),
            ("IsActive", "Select a status."));
    }

    [Fact]
    public void ContactValidatorsApplySharedUsPhoneFaxAndExtensionRules()
    {
        var clientId = Guid.NewGuid();
        var validContact = new CreateClientContactValidator().Validate(
            new CreateClientContactCommand(clientId, "General", "Person", "Friend",
                "(612) 555-0147", "person@example.com", null, true));
        Assert.True(validContact.IsValid);

        var invalidContact = new CreateClientContactValidator().Validate(
            new CreateClientContactCommand(clientId, "General", "Person", "Friend",
                "612-555", null, null, true));
        Assert.Contains(invalidContact.Errors, error =>
            error.PropertyName == "Phone" && error.ErrorMessage.Contains("10 US digits"));

        var invalidCoordinator = new CreateClientCareCoordinatorValidator().Validate(
            new CreateClientCareCoordinatorCommand(clientId, "Coordinator", null,
                "6125550147", "ext-4", "6125550199", null, true));
        Assert.Contains(invalidCoordinator.Errors, error =>
            error.PropertyName == "PhoneExtension" && error.ErrorMessage.Contains("digits only"));
    }

    [Fact]
    public void MutationAuditEventsUseApprovedNamesIdentifiersAndMetadata()
    {
        var clientId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var coordinator = new CareCoordinatorItem(resourceId, "Name", "Company", "Phone",
            "Extension", "Fax", "Email", true);
        var contact = new ClientContactItem(resourceId, ClientContactType.General, "Name",
            "Relationship", "Phone", "Email", "Address", true);
        var party = new ResponsiblePartyItem(resourceId, "Name", "Relationship", "Phone",
            "Email", "Address", false, false, true);
        var clinical = new ClinicalContactItem(resourceId, "Name", "Phone", "Email",
            "Secondary", "Secondary Phone", "Secondary Address", "Npi", "Fax", "Address",
            "Postal", "City", "State", true);

        AssertSuccessfulAudit(
            new CreateClientCareCoordinatorCommand(clientId, null, null, null, null, null, null, null)
                .CreateAuditEvent(ClientCareCoordinatorResult.Changed(coordinator)),
            "ClientCareCoordinator.Created", "ClientCareCoordinator", clientId, resourceId);
        AssertSuccessfulAudit(
            new UpdateClientCareCoordinatorCommand(clientId, resourceId, null, null, null, null,
                null, null, null).CreateAuditEvent(
                    ClientCareCoordinatorResult.Changed(coordinator)),
            "ClientCareCoordinator.Updated", "ClientCareCoordinator", clientId, resourceId);
        AssertSuccessfulAudit(
            new CreateClientContactCommand(clientId, null, null, null, null, null, null, null)
                .CreateAuditEvent(ClientContactResult.Changed(contact)),
            "ClientContact.Created", "ClientContact", clientId, resourceId);
        AssertSuccessfulAudit(
            new UpdateClientContactCommand(clientId, resourceId, null, null, null, null, null,
                null, null).CreateAuditEvent(ClientContactResult.Changed(contact)),
            "ClientContact.Updated", "ClientContact", clientId, resourceId);
        AssertSuccessfulAudit(
            new CreateClientResponsiblePartyCommand(clientId, null, null, null, null, null, null,
                null, null).CreateAuditEvent(ClientResponsiblePartyResult.Changed(party)),
            "ClientResponsibleParty.Created", "ClientResponsibleParty", clientId, resourceId);
        AssertSuccessfulAudit(
            new UpdateClientResponsiblePartyCommand(clientId, resourceId, null, null, null, null,
                null, null, null, null).CreateAuditEvent(
                    ClientResponsiblePartyResult.Changed(party)),
            "ClientResponsibleParty.Updated", "ClientResponsibleParty", clientId, resourceId);
        AssertSuccessfulAudit(
            new CreateConsultationProviderCommand(clientId, null, null, null, null, null, null, null)
                .CreateAuditEvent(ClientClinicalContactResult.Changed(clinical)),
            "ClientConsultationProvider.Created", "ClientConsultationProvider", clientId,
            resourceId);
        AssertSuccessfulAudit(
            new UpdateConsultationProviderCommand(clientId, resourceId, null, null, null, null,
                null, null, null).CreateAuditEvent(ClientClinicalContactResult.Changed(clinical)),
            "ClientConsultationProvider.Updated", "ClientConsultationProvider", clientId,
            resourceId);
        AssertSuccessfulAudit(
            new CreatePhysicianCommand(clientId, null, null, null, null, null, null, null, null,
                null).CreateAuditEvent(ClientClinicalContactResult.Changed(clinical)),
            "ClientPhysician.Created", "ClientPhysician", clientId, resourceId);
        AssertSuccessfulAudit(
            new UpdatePhysicianCommand(clientId, resourceId, null, null, null, null, null, null,
                null, null, null).CreateAuditEvent(ClientClinicalContactResult.Changed(clinical)),
            "ClientPhysician.Updated", "ClientPhysician", clientId, resourceId);

        var failed = new UpdateClientContactCommand(clientId, resourceId, null, null, null, null,
            null, null, null).CreateAuditEvent(ClientContactResult.Changed(null));
        Assert.Equal(AuditOutcome.Failed, failed.Outcome);
        Assert.Equal(resourceId.ToString("N"), failed.ResourceId);
        Assert.Equal([new KeyValuePair<string, string>("reasonCode", "not-found")],
            failed.Metadata.Entries);
    }

    private static void AssertErrors(
        ValidationResult result,
        params (string Key, string Message)[] expected)
    {
        var actual = result.Errors
            .Select(error => (Key: error.PropertyName, Message: error.ErrorMessage))
            .OrderBy(error => error.Key, StringComparer.Ordinal)
            .ThenBy(error => error.Message, StringComparer.Ordinal)
            .ToArray();
        var orderedExpected = expected
            .OrderBy(error => error.Key, StringComparer.Ordinal)
            .ThenBy(error => error.Message, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(orderedExpected, actual);
    }

    private static void AssertSuccessfulAudit(
        AuditEvent auditEvent,
        string action,
        string resourceType,
        Guid clientId,
        Guid resourceId)
    {
        Assert.Equal(action, auditEvent.Action);
        Assert.Equal(resourceType, auditEvent.ResourceType);
        Assert.Equal(resourceId.ToString("N"), auditEvent.ResourceId);
        Assert.Equal(AuditOutcome.Succeeded, auditEvent.Outcome);
        Assert.Equal([new KeyValuePair<string, string>("clientId", clientId.ToString("N"))],
            auditEvent.Metadata.Entries);
    }
}
