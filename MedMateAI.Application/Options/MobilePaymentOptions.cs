namespace MedMateAI.Application.Options;

public sealed class MobilePaymentOptions
{
    public const string SectionName = "MobilePayment";

    public string ReturnUrl { get; set; } = string.Empty;

    public string CancelUrl { get; set; } = string.Empty;

    public string DeepLinkUrl { get; set; } =
        "sep490mbmedicalaiassistant://payment-result";
}
