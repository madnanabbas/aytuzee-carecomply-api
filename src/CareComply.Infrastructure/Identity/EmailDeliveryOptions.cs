namespace CareComply.Infrastructure.Identity;

public sealed class EmailDeliveryOptions
{
    public const string SectionName = "EmailDelivery";

    public string AzureCommunicationServicesEndpoint { get; init; } = string.Empty;

    public string SenderAddress { get; init; } = string.Empty;

    public string SenderDisplayName { get; init; } = "Aytuzee Care Comply";
}
