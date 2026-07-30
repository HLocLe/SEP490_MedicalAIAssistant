using System.Text.Encodings.Web;
using MedMateAI.Application.Common;
using MedMateAI.Application.IService;
using MedMateAI.Application.Models.Notifications;
using MedMateAI.Application.Options;
using Microsoft.Extensions.Options;

namespace MedMateAI.Infrastructure.BackgroundJobs.RecoveryPlans;

public sealed class NotificationEmailRenderer : INotificationEmailRenderer
{
    private readonly string? _loginUrl;

    public NotificationEmailRenderer(IOptions<FrontendOptions> frontendOptions)
    {
        _loginUrl = BuildLoginUrl(frontendOptions.Value.BaseUrl);
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

    private string BuildRecoveryPlanHtml(string heading, string message)
    {
        var loginAction = "<p>Vui lòng mở ứng dụng và đăng nhập để xem chi tiết.</p>";

        if (_loginUrl is not null)
        {
            var encodedUrl = HtmlEncoder.Default.Encode(_loginUrl);
            loginAction =
                $"""<p><a href="{encodedUrl}">Đăng nhập để xem kế hoạch hồi phục</a></p>""";
        }

        return
            $"""
            <div style="font-family:Arial,sans-serif;line-height:1.6">
              <h2>{heading}</h2>
              <p>{message}</p>
              {loginAction}
            </div>
            """;
    }

    private static string? BuildLoginUrl(string? baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsedBaseUrl)
            || parsedBaseUrl.Scheme is not ("http" or "https"))
        {
            return null;
        }

        var normalizedBaseUrl = parsedBaseUrl.AbsoluteUri.EndsWith(
            "/",
            StringComparison.Ordinal)
            ? parsedBaseUrl
            : new Uri($"{parsedBaseUrl.AbsoluteUri}/", UriKind.Absolute);

        return new Uri(normalizedBaseUrl, "login").AbsoluteUri;
    }
}
