using System.Security.Cryptography;
using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;

namespace CareComply.Infrastructure.Clients;

internal sealed class ClientChangeDetector(IClientSensitiveData sensitiveData)
{
    public static bool IdentityChanged(Client client, ClientIdentityInput input) =>
        !Equal(client.FirstName, input.FirstName)
        || !Equal(client.MiddleName, input.MiddleName)
        || !Equal(client.LastName, input.LastName)
        || !Equal(client.PreferredName, input.PreferredName)
        || client.DateOfBirth != input.DateOfBirth
        || client.Gender != input.Gender
        || !Equal(client.PmiNumber, input.PmiNumber, StringComparison.OrdinalIgnoreCase);

    public static bool ContactChanged(Client client, ClientContactInput input) =>
        !Equal(client.Email, input.Email, StringComparison.OrdinalIgnoreCase)
        || !Equal(client.PrimaryPhone, input.PrimaryPhone)
        || !Equal(client.SecondaryPhone, input.SecondaryPhone)
        || !Equal(client.AddressLine1, input.AddressLine1)
        || !Equal(client.AddressLine2, input.AddressLine2)
        || !Equal(client.City, input.City)
        || !Equal(client.State, input.State)
        || !Equal(client.PostalCode, input.PostalCode)
        || !Equal(client.County, input.County)
        || !Equal(client.Country, input.Country);

    public bool SsnChanged(Client client, string? requestedSsn) =>
        !SsnMatches(client.ProtectedSsn, requestedSsn);

    public static bool CareChanged(
        Client client,
        ClientCareSetupInput input,
        string? insuranceProvider) =>
        client.IntakeDate != input.IntakeDate
        || client.SocDate != input.SocDate
        || !Equal(client.ReferralSource, input.ReferralSource)
        || !Equal(client.Location, input.Location)
        || !Equal(client.InsuranceProvider, insuranceProvider)
        || client.IsOnboarding != input.IsOnboarding
        || client.EvvAuthorizedHoursPerWeek != input.EvvAuthorizedHoursPerWeek;

    public static bool SafetyChanged(Client client, ClientSafetyInput input) =>
        !Equal(client.SafetyMeasures, input.SafetyMeasures)
        || !Equal(client.SpecialEquipment, input.SpecialEquipment)
        || !Equal(client.Allergies, input.Allergies)
        || !Equal(client.OtherServices, input.OtherServices);

    internal static bool Equal(
        string? current,
        string? requested,
        StringComparison comparison = StringComparison.Ordinal) =>
        string.Equals(Normalize(current), Normalize(requested), comparison);

    private bool SsnMatches(string protectedSsn, string? requestedSsn)
    {
        // Omitted SSN means "leave unchanged" so ordinary client edits cannot clear protected data.
        if (requestedSsn is null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(protectedSsn))
        {
            return string.IsNullOrWhiteSpace(requestedSsn);
        }

        if (string.IsNullOrWhiteSpace(requestedSsn))
        {
            return false;
        }

        try
        {
            return string.Equals(
                sensitiveData.Unprotect(protectedSsn),
                requestedSsn,
                StringComparison.Ordinal);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim();
}
