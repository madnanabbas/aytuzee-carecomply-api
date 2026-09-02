namespace CareComply.Domain.Clients;

public sealed class ClientContact
{
    public const int MaximumNameLength = 200;
    public const int MaximumRelationshipLength = 100;
    public const int MaximumPhoneLength = 30;
    public const int MaximumEmailLength = 320;
    public const int MaximumNotesLength = 1000;

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
