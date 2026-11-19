namespace CareComply.Infrastructure.Forms;

public sealed class FormContentStorageOptions
{
    public const string SectionName = "FormContentStorage";

    public const long DefaultMaximumUploadSizeBytes = 10 * 1024 * 1024;

    public string Provider { get; set; } = FormContentStorageProviders.Local;

    public string LocalRootPath { get; set; } = string.Empty;

    public string ClientDocumentLocalRootPath { get; set; } = string.Empty;

    public long MaximumUploadSizeBytes { get; set; } = DefaultMaximumUploadSizeBytes;

    public long ClientDocumentMaximumUploadSizeBytes { get; set; } = DefaultMaximumUploadSizeBytes;

    public AzureBlobFormContentStoreOptions AzureBlob { get; set; } = new();
}

public sealed class AzureBlobFormContentStoreOptions
{
    public string AccountEndpoint { get; set; } = string.Empty;

    public string ContainerName { get; set; } = string.Empty;

    public string ClientDocumentContainerName { get; set; } = string.Empty;

    public int MaximumRetries { get; set; } = 3;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan MaximumRetryDelay { get; set; } = TimeSpan.FromSeconds(4);

    public TimeSpan NetworkTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

public static class FormContentStorageProviders
{
    public const string Local = "Local";
    public const string AzureBlob = "AzureBlob";
}
