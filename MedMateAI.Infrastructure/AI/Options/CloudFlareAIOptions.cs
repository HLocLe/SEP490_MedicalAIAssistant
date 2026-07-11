namespace MedMateAI.Infrastructure.AI.Options;

public sealed class CloudFlareAIOptions
{
    public const string SectionName = "CloudFlareAI";

    public string Endpoint { get; set; } = string.Empty;

    public string GatewayId { get; set; } = "default";

    public string Model { get; set; } = string.Empty;

    public string ApiToken { get; set; } = string.Empty;

    public decimal Temperature { get; set; } = 0.2m;
}
