namespace CareComply.Domain.Forms;

public enum FormTemplateOwnerType
{
    Platform = 1,
    Company = 2,
}

public enum FormTemplateVersionStatus
{
    Draft = 1,
    Published = 2,
    Retired = 3,
}

public enum FormTechnology
{
    AcroForm = 1,
    HybridXfaAcroForm = 2,
    Unsupported = 3,
}

public enum FormFieldType
{
    Text = 1,
    CheckBox = 2,
    RadioButton = 3,
    Choice = 4,
    Button = 5,
    Signature = 6,
    Unknown = 7,
}

public enum FormFieldPurpose
{
    Data = 1,
    Manual = 2,
    Signature = 3,
    Attestation = 4,
}
