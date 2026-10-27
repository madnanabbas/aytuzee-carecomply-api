namespace CareComply.Infrastructure.Forms;

public sealed class LocalFormContentStoreOptions
{
    public const string SectionName = "FormContentStorage";

    public string LocalRootPath { get; set; } = string.Empty;
}
