namespace MedMateAI.Infrastructure.Email.Brevo.Options;

public sealed class BrevoOptions
{
    public const string SectionName = "Brevo";

    public string ApiKey { get; set; } = string.Empty;

    public string SenderEmail { get; set; } = string.Empty;

    public string SenderName { get; set; } = string.Empty;

    public string ApiUrl { get; set; } = "https://api.brevo.com/v3/smtp/email";
}
