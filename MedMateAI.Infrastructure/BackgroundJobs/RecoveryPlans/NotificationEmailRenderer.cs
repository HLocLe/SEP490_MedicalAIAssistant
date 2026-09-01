using System.Text.Encodings.Web;
using System.Globalization;
using MedMateAI.Application.Common;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.Notifications;
using MedMateAI.Application.Options;
using Microsoft.Extensions.Options;

namespace MedMateAI.Infrastructure.BackgroundJobs.RecoveryPlans;

public sealed class NotificationEmailRenderer : INotificationEmailRenderer
{
    private readonly string? _loginUrl;
    private readonly string? _saleCtaUrl;
    private readonly SaleCampaignNotificationOptions _saleOptions;

    public NotificationEmailRenderer(
        IOptions<FrontendOptions> frontendOptions,
        IOptions<SaleCampaignNotificationOptions> saleOptions)
    {
        _loginUrl = BuildFrontendUrl(frontendOptions.Value.BaseUrl, "login");
        _saleCtaUrl = BuildFrontendUrl(
            frontendOptions.Value.BaseUrl,
            "subscription");
        _saleOptions = saleOptions.Value;
    }

    public NotificationEmailContent RenderRecoveryPlanReady()
    {
        return new NotificationEmailContent(
            RecoveryPlanNotificationContent.ReadyTitle,
            BuildRecoveryPlanHtml(
                RecoveryPlanNotificationContent.ReadyTitle,
                RecoveryPlanNotificationContent.ReadyMessage));
    }

    public NotificationEmailContent RenderRecoveryPlanCompleted()
    {
        return new NotificationEmailContent(
            RecoveryPlanNotificationContent.CompletedTitle,
            BuildRecoveryPlanHtml(
                RecoveryPlanNotificationContent.CompletedTitle,
                RecoveryPlanNotificationContent.CompletedMessage));
    }

    public NotificationEmailContent RenderRecoveryPlanCancelled(
        string? planName,
        string cancellationReasonCode,
        string? cancellationReason)
    {
        var planParagraph = string.Empty;
        if (!string.IsNullOrWhiteSpace(planName))
        {
            var encodedPlanName = HtmlEncoder.Default.Encode(planName);
            planParagraph = $"<p>Kế hoạch: <strong>{encodedPlanName}</strong>.</p>";
        }

        var reasonLabel = RecoveryPlanCancellationReasons.GetDisplayLabel(
            cancellationReasonCode) ?? "Không xác định";
        var encodedReasonLabel = HtmlEncoder.Default.Encode(reasonLabel);
        var reasonParagraph = $"<p>Lý do: <strong>{encodedReasonLabel}</strong>.</p>";

        var noteParagraph = string.Empty;
        if (!string.IsNullOrWhiteSpace(cancellationReason))
        {
            var encodedReason = HtmlEncoder.Default.Encode(cancellationReason);
            noteParagraph = $"<p>Ghi chú của bạn: {encodedReason}</p>";
        }

        var html =
            $"""
            <div style="font-family:Arial,sans-serif;line-height:1.6">
              <h2>{RecoveryPlanCancellationNotificationContent.Title}</h2>
              <p>{RecoveryPlanCancellationNotificationContent.Message}</p>
              {planParagraph}
              {reasonParagraph}
              {noteParagraph}
              <p>Bạn có thể yêu cầu kế hoạch mới nếu hạn mức còn lại cho phép.</p>
              {BuildLoginAction()}
            </div>
            """;

        return new NotificationEmailContent(
            RecoveryPlanCancellationNotificationContent.Title,
            html);
    }

    public NotificationEmailContent RenderMedicationReminder(
        string medicineName,
        string? dosageInstruction)
    {
        var encodedMedicineName = HtmlEncoder.Default.Encode(medicineName);
        var dosageParagraph = string.Empty;

        if (!string.IsNullOrWhiteSpace(dosageInstruction))
        {
            var encodedDosage = HtmlEncoder.Default.Encode(dosageInstruction);
            dosageParagraph = $"<p>Hướng dẫn bạn đã lưu: {encodedDosage}</p>";
        }

        var encodedDisclaimer = HtmlEncoder.Default.Encode(
            MedicationReminderNotificationContent.Disclaimer);
        var html =
            $"""
            <div style="font-family:Arial,sans-serif;line-height:1.6">
              <h2>{MedicationReminderNotificationContent.Title}</h2>
              <p>Đã đến thời gian nhắc dùng: <strong>{encodedMedicineName}</strong>.</p>
              {dosageParagraph}
              <p>{encodedDisclaimer}</p>
            </div>
            """;

        return new NotificationEmailContent(
            MedicationReminderNotificationContent.Title,
            html);
    }

    public NotificationEmailContent RenderSaleCampaignAnnouncement(
        SaleCampaignAnnouncementContext context,
        SaleCampaignNotificationContent content)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(content);

        var encodedTitle = HtmlEncoder.Default.Encode(content.Title);
        var encodedBody = HtmlEncoder.Default.Encode(content.Body);
        var encodedCampaignName = HtmlEncoder.Default.Encode(
            context.CampaignName.Trim());
        var description = string.IsNullOrWhiteSpace(context.Description)
            ? string.Empty
            : $"<p>{HtmlEncoder.Default.Encode(context.Description.Trim())}</p>";
        var badge = string.IsNullOrWhiteSpace(context.BadgeText)
            ? string.Empty
            : $"<p><strong>{HtmlEncoder.Default.Encode(context.BadgeText.Trim())}</strong></p>";
        var offerItems = context.Offers
            .Take(_saleOptions.MaxOffersInEmail)
            .Select(BuildOfferItem)
            .ToList();
        var moreOffers = context.Offers.Count > offerItems.Count
            ? "<p>...và các ưu đãi khác.</p>"
            : string.Empty;
        var endAt = AsUtc(context.EndAt).ToString(
            "dd/MM/yyyy HH:mm 'UTC'",
            CultureInfo.InvariantCulture);
        var html =
            $"""
            <div style="font-family:Arial,sans-serif;line-height:1.6">
              <h2>{encodedTitle}</h2>
              <p>{encodedBody}</p>
              <p>Chương trình <strong>{encodedCampaignName}</strong> đang diễn ra.</p>
              {badge}
              {description}
              <p><strong>Ưu đãi hiện dành cho bạn:</strong></p>
              <ul>{string.Join(string.Empty, offerItems)}</ul>
              {moreOffers}
              <p>Chương trình kết thúc lúc {endAt}.</p>
              {BuildSaleAction()}
            </div>
            """;

        return new NotificationEmailContent(content.Title, html);
    }

    private string BuildRecoveryPlanHtml(string heading, string message)
    {
        return
            $"""
            <div style="font-family:Arial,sans-serif;line-height:1.6">
              <h2>{heading}</h2>
              <p>{message}</p>
              {BuildLoginAction()}
            </div>
            """;
    }

    private string BuildLoginAction()
    {
        if (_loginUrl is null)
        {
            return "<p>Vui lòng mở ứng dụng và đăng nhập để xem chi tiết.</p>";
        }

        var encodedUrl = HtmlEncoder.Default.Encode(_loginUrl);
        return $"""<p><a href="{encodedUrl}">Đăng nhập để xem kế hoạch hồi phục</a></p>""";
    }

    private string BuildSaleAction()
    {
        if (_saleCtaUrl is null)
        {
            return string.Empty;
        }

        var encodedUrl = HtmlEncoder.Default.Encode(_saleCtaUrl);
        return $"""<p><a href="{encodedUrl}">Xem ưu đãi ngay</a></p>""";
    }

    private static string? BuildFrontendUrl(
        string? baseUrl,
        string relativePath)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)
            || !Uri.TryCreate(
                baseUrl.Trim(),
                UriKind.Absolute,
                out var parsedBaseUrl)
            || parsedBaseUrl.Scheme is not ("http" or "https"))
        {
            return null;
        }

        var normalizedBaseUrl = parsedBaseUrl.AbsoluteUri.EndsWith(
            "/",
            StringComparison.Ordinal)
            ? parsedBaseUrl
            : new Uri($"{parsedBaseUrl.AbsoluteUri}/", UriKind.Absolute);

        return new Uri(normalizedBaseUrl, relativePath).AbsoluteUri;
    }

    private static string BuildOfferItem(SaleCampaignAnnouncementOffer offer)
    {
        var planName = HtmlEncoder.Default.Encode(
            string.IsNullOrWhiteSpace(offer.PlanName)
                ? "Gói phù hợp"
                : offer.PlanName.Trim());
        var parts = new List<string>();
        if (offer.EffectivePrice < offer.OriginalPrice)
        {
            parts.Add(
                $"<s>{FormatVnd(offer.OriginalPrice)}</s> → <strong>{FormatVnd(offer.EffectivePrice)}</strong>");
        }
        else
        {
            parts.Add(FormatVnd(offer.OriginalPrice));
        }

        if (offer.BonusCredit > 0)
        {
            parts.Add($"+{offer.BonusCredit} lượt (tổng nhận {offer.GrantedCredit} lượt)");
        }

        return $"<li><strong>{planName}</strong>: {string.Join("; ", parts)}</li>";
    }

    private static string FormatVnd(decimal amount)
    {
        return $"{amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))}đ";
    }

    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime()
        };
    }
}
