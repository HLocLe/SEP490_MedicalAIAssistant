namespace MedMateAI.Application.Models.Notifications;

public sealed record NotificationEmailContent(
    string Subject,
    string HtmlBody);
