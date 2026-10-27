namespace CareComply.Domain.Forms;

public sealed class FormTemplateFieldMapping
{
    public const int MaximumPdfFieldNameLength = 512;
    public const int MaximumBindingKeyLength = 200;
    public const int MaximumFormatKeyLength = 100;

    private FormTemplateFieldMapping()
    {
    }

    public Guid Id { get; private set; }
    public Guid FormTemplateVersionId { get; private set; }
    public string PdfFieldName { get; private set; } = string.Empty;
    public FormFieldType PdfFieldType { get; private set; }
    public FormFieldPurpose FieldPurpose { get; private set; }
    public string? BindingKey { get; private set; }
    public string? FormatKey { get; private set; }
    public bool IsUserEditable { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static FormTemplateFieldMapping Create(
        Guid id,
        Guid formTemplateVersionId,
        string pdfFieldName,
        FormFieldType pdfFieldType,
        FormFieldPurpose fieldPurpose,
        string? bindingKey,
        string? formatKey,
        bool isUserEditable,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A field mapping identifier is required.", nameof(id));
        if (formTemplateVersionId == Guid.Empty)
            throw new ArgumentException("A form template version identifier is required.", nameof(formTemplateVersionId));
        if (!Enum.IsDefined(pdfFieldType)) throw new ArgumentOutOfRangeException(nameof(pdfFieldType));
        if (!Enum.IsDefined(fieldPurpose)) throw new ArgumentOutOfRangeException(nameof(fieldPurpose));

        var normalizedBindingKey = NormalizeOptional(
            bindingKey,
            MaximumBindingKeyLength,
            nameof(bindingKey));
        if (fieldPurpose == FormFieldPurpose.Data && normalizedBindingKey is null)
            throw new ArgumentException("A data field requires an approved binding key.", nameof(bindingKey));
        if (fieldPurpose != FormFieldPurpose.Data && normalizedBindingKey is not null)
            throw new ArgumentException("Only data fields may have a binding key.", nameof(bindingKey));

        var timestamp = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        return new FormTemplateFieldMapping
        {
            Id = id,
            FormTemplateVersionId = formTemplateVersionId,
            PdfFieldName = NormalizeRequired(
                pdfFieldName,
                MaximumPdfFieldNameLength,
                nameof(pdfFieldName)),
            PdfFieldType = pdfFieldType,
            FieldPurpose = fieldPurpose,
            BindingKey = normalizedBindingKey,
            FormatKey = NormalizeOptional(formatKey, MaximumFormatKeyLength, nameof(formatKey)),
            IsUserEditable = isUserEditable,
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp,
        };
    }

    private static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maximumLength, string parameterName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized)) return null;
        if (normalized.Length > maximumLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new ArgumentException("The timestamp must be UTC.", parameterName);
        return value;
    }
}
