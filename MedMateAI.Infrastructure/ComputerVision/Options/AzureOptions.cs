namespace MedMateAI.Infrastructure.ComputerVision.Options;

public sealed class AzureOptions
{
    public const string SectionName = "Azure";

    public string Endpoint { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string ModelId { get; set; } = "prebuilt-layout";

    public string ApiVersion { get; set; } = "2024-11-30";
}
