using System.Collections.ObjectModel;

namespace CareComply.Application.Auditing;

public sealed class AuditMetadata
{
    public const int MaximumEntryCount = 10;
    public const int MaximumKeyLength = 64;
    public const int MaximumValueLength = 256;
    public const int MaximumSerializedLength = 2048;

    private static readonly HashSet<string> ProhibitedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "accessToken",
        "antiforgeryToken",
        "authorizationHeader",
        "connectionString",
        "cookie",
        "credential",
        "documentContent",
        "exception",
        "fileContent",
        "password",
        "passwordHash",
        "refreshToken",
        "requestBody",
        "secret",
        "securityStamp",
        "stackTrace",
        "token",
    };

    private readonly ReadOnlyCollection<KeyValuePair<string, string>> _entries;

    private AuditMetadata(KeyValuePair<string, string>[] entries)
    {
        _entries = Array.AsReadOnly(entries);
    }

    public static AuditMetadata Empty { get; } = new([]);

    public IReadOnlyList<KeyValuePair<string, string>> Entries => _entries;

    public static AuditMetadata Create(params (string Key, string Value)[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Length > MaximumEntryCount)
        {
            throw new ArgumentException(
                $"Audit metadata may contain at most {MaximumEntryCount} entries.",
                nameof(entries));
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var validated = new KeyValuePair<string, string>[entries.Length];

        for (var index = 0; index < entries.Length; index++)
        {
            var (key, value) = entries[index];
            ValidateKey(key);
            ValidateValue(value);

            if (!keys.Add(key))
            {
                throw new ArgumentException(
                    "Audit metadata keys must be unique.",
                    nameof(entries));
            }

            validated[index] = new KeyValuePair<string, string>(key, value);
        }

        Array.Sort(
            validated,
            static (left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));
        return validated.Length == 0 ? Empty : new AuditMetadata(validated);
    }

    public override string ToString() =>
        "AuditMetadata { Values = [OMITTED] }";

    private static void ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (key.Length > MaximumKeyLength
            || !char.IsAsciiLetter(key[0])
            || key.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new ArgumentException(
                "Audit metadata keys must be bounded server-owned alphanumeric names.",
                nameof(key));
        }

        if (ProhibitedKeys.Contains(key))
        {
            throw new ArgumentException(
                "The audit metadata key represents prohibited sensitive information.",
                nameof(key));
        }
    }

    private static void ValidateValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length > MaximumValueLength || value.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Audit metadata values must be bounded and contain no control characters.",
                nameof(value));
        }
    }
}
