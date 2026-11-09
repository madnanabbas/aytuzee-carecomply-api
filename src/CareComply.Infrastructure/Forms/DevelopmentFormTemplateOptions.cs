namespace CareComply.Infrastructure.Forms;

public sealed class DevelopmentFormTemplateOptions
{
    public const string SectionName = "DevelopmentFixtures:FormTemplates";

    public bool Enabled { get; set; }
    public string SourcePdfPath { get; set; } = string.Empty;
}
