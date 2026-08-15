namespace MedMateAI.Infrastructure.SMS.Stringee.Options;

public sealed class StringeeOptions
{
    public const string SectionName = "Stringee";

    public string ProjectId { get; set; } = string.Empty;

    public string ApiKeySid { get; set; } = string.Empty;

    public string ApiKeySecret { get; set; } = string.Empty;

    public string FromSender { get; set; } = string.Empty;
}
