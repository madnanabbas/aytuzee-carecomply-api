namespace CareComply.Domain.Clients;

public sealed class ClientLanguagePreference
{
    public const int MaximumLanguageCodeLength = 20;
    public const int MaximumLanguageNameLength = 100;

    private ClientLanguagePreference()
    {
    }

    public ClientLanguagePreference(Guid clientId, string languageCode, string languageName, bool isPrimary)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("A client identifier is required.", nameof(clientId));
        ClientId = clientId;
        LanguageCode = NormalizeRequired(languageCode, MaximumLanguageCodeLength, nameof(languageCode));
        LanguageName = NormalizeRequired(languageName, MaximumLanguageNameLength, nameof(languageName));
        IsPrimary = isPrimary;
    }

    public Guid ClientId { get; private set; }
    public string LanguageCode { get; private set; } = string.Empty;
    public string LanguageName { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }

    public void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }
}
