namespace CareComply.Infrastructure.Forms;

internal static class FormContentObjectKey
{
    public static void Validate(string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        if (!Guid.TryParseExact(objectKey, "N", out var objectId)
            || !string.Equals(objectKey, objectId.ToString("N"), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical opaque form-content object key is required.",
                nameof(objectKey));
        }
    }
}
