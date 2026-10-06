namespace CareComply.Domain.Clients;

public sealed class ClientContact
{
    public const int MaximumNameLength = 200;
    public const int MaximumRelationshipLength = 100;
    public const int MaximumPhoneLength = 30;
    public const int MaximumEmailLength = 320;
    public const int MaximumNotesLength = 1000;
    public const int MaximumAddressLength = 500;
    public const int MaximumCompanyLength = 200;
    public const int MaximumExtensionLength = 15;
    public const int MaximumPostalCodeLength = 10;
    public const int MaximumCityLength = 100;
    public const int MaximumStateLength = 100;

    private ClientContact()
    {
    }

    public ClientContact(
        Guid id,
        Guid clientId,
        ClientContactType type,
        string name,
        string? relationship,
        string? phone,
        string? email,
        bool isPrimary,
        bool isEmergencyContact,
        DateOnly? effectiveFrom,
        DateOnly? effectiveTo,
        string? notes)
    {
        if (id == Guid.Empty) throw new ArgumentException("A contact identifier is required.", nameof(id));
        if (clientId == Guid.Empty) throw new ArgumentException("A client identifier is required.", nameof(clientId));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (effectiveFrom.HasValue && effectiveTo.HasValue && effectiveTo < effectiveFrom)
            throw new ArgumentException("Effective-to date cannot be before effective-from date.", nameof(effectiveTo));

        Id = id;
        ClientId = clientId;
        Type = type;
        Name = NormalizeRequired(name, MaximumNameLength, nameof(name));
        Relationship = Normalize(relationship, MaximumRelationshipLength, nameof(relationship));
        Phone = Normalize(phone, MaximumPhoneLength, nameof(phone));
        Email = Normalize(email, MaximumEmailLength, nameof(email));
        IsPrimary = isPrimary;
        IsEmergencyContact = isEmergencyContact;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        Notes = Normalize(notes, MaximumNotesLength, nameof(notes));
    }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public ClientContactType Type { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Relationship { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }
    public bool IsEmergencyContact { get; private set; }
    public DateOnly? EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string Notes { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public bool? LivesWithClient { get; private set; }
    public bool? HasSpenddown { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string CompanyName { get; private set; } = string.Empty;
    public string PhoneExtension { get; private set; } = string.Empty;
    public string Fax { get; private set; } = string.Empty;
    public string SecondaryName { get; private set; } = string.Empty;
    public string SecondaryPhone { get; private set; } = string.Empty;
    public string SecondaryAddress { get; private set; } = string.Empty;
    public string Npi { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;

    public void UpdateConsultationProvider(string name, string? phone, string? email,
        string? supportProviderName, string? supportProviderPhone, string? supportProviderAddress, bool isActive)
    {
        if (Type != ClientContactType.ConsultationProvider)
            throw new InvalidOperationException("Not a consultation provider.");
        Name = NormalizeRequired(name, MaximumNameLength, nameof(name));
        Phone = Normalize(phone, MaximumPhoneLength, nameof(phone));
        Email = Normalize(email, MaximumEmailLength, nameof(email));
        SecondaryName = Normalize(supportProviderName, MaximumNameLength, nameof(supportProviderName));
        SecondaryPhone = Normalize(supportProviderPhone, MaximumPhoneLength, nameof(supportProviderPhone));
        SecondaryAddress = Normalize(supportProviderAddress, MaximumAddressLength, nameof(supportProviderAddress));
        IsActive = isActive;
    }

    public void UpdatePhysician(string name, string? npi, string? phone, string? fax,
        string? address, string? postalCode, string? city, string? state, bool isActive)
    {
        if (Type != ClientContactType.Physician)
            throw new InvalidOperationException("Not a physician.");
        Name = NormalizeRequired(name, MaximumNameLength, nameof(name));
        var normalizedNpi = Normalize(npi, 10, nameof(npi));
        if (normalizedNpi.Length > 0 && (normalizedNpi.Length != 10 || !normalizedNpi.All(char.IsAsciiDigit)))
            throw new ArgumentException("NPI must contain exactly 10 digits.", nameof(npi));
        Npi = normalizedNpi;
        Phone = Normalize(phone, MaximumPhoneLength, nameof(phone));
        Fax = Normalize(fax, MaximumPhoneLength, nameof(fax));
        Address = Normalize(address, MaximumAddressLength, nameof(address));
        PostalCode = Normalize(postalCode, MaximumPostalCodeLength, nameof(postalCode));
        City = Normalize(city, MaximumCityLength, nameof(city));
        State = Normalize(state, MaximumStateLength, nameof(state));
        IsActive = isActive;
    }

    public void UpdateCareCoordinator(string name, string? companyName, string? phone,
        string? phoneExtension, string? fax, string? email, bool isActive)
    {
        if (Type != ClientContactType.CareCoordinator)
            throw new InvalidOperationException("Not a care coordinator.");
        Name = NormalizeRequired(name, MaximumNameLength, nameof(name));
        CompanyName = Normalize(companyName, MaximumCompanyLength, nameof(companyName));
        Phone = Normalize(phone, MaximumPhoneLength, nameof(phone));
        PhoneExtension = Normalize(phoneExtension, MaximumExtensionLength, nameof(phoneExtension));
        Fax = Normalize(fax, MaximumPhoneLength, nameof(fax));
        Email = Normalize(email, MaximumEmailLength, nameof(email));
        IsActive = isActive;
    }

    public void UpdateContactDetails(string name, string relationship, string phone, string? email,
        string? address, bool isActive)
    {
        if (Type is not (ClientContactType.General or ClientContactType.Emergency))
            throw new InvalidOperationException("Only general and emergency contacts can be edited here.");
        Name = NormalizeRequired(name, MaximumNameLength, nameof(name));
        Relationship = NormalizeRequired(relationship, MaximumRelationshipLength, nameof(relationship));
        Phone = NormalizeRequired(phone, MaximumPhoneLength, nameof(phone));
        Email = Normalize(email, MaximumEmailLength, nameof(email));
        Address = Normalize(address, MaximumAddressLength, nameof(address));
        IsActive = isActive;
    }

    public void UpdateResponsibleParty(string name, string relationship, string phone, string? email,
        string? address, bool livesWithClient, bool hasSpenddown, bool isActive)
    {
        if (Type != ClientContactType.ResponsibleParty) throw new InvalidOperationException("Not a responsible party.");
        Name = NormalizeRequired(name, MaximumNameLength, nameof(name));
        Relationship = NormalizeRequired(relationship, MaximumRelationshipLength, nameof(relationship));
        Phone = NormalizeRequired(phone, MaximumPhoneLength, nameof(phone));
        Email = Normalize(email, MaximumEmailLength, nameof(email));
        Address = Normalize(address, MaximumAddressLength, nameof(address));
        LivesWithClient = livesWithClient;
        HasSpenddown = hasSpenddown;
        IsActive = isActive;
    }

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Normalize(value, maxLength, parameterName);
    }

    private static string Normalize(string? value, int maxLength, string parameterName)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length > maxLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }
}
