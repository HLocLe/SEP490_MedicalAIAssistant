using MedMateAI.Application.Common;

namespace MedMateAI.Tests.Services;

[TestFixture]
public class ConsultationReminderEmailBuilderTests
{
    [Test]
    public void BuildHtml_ContainsAppointmentAndDepartment()
    {
        var appointment = new DateTime(2026, 8, 20, 3, 0, 0, DateTimeKind.Utc);

        var html = ConsultationReminderEmailBuilder.BuildHtml(
            "Nguyễn Văn A",
            new DateOnly(1990, 1, 15),
            "Tim mạch",
            "Bệnh viện A",
            appointment);

        Assert.That(html, Does.Contain("Nguyễn Văn A"));
        Assert.That(html, Does.Contain("Tim mạch"));
        Assert.That(html, Does.Contain("Bệnh viện A"));
        Assert.That(html, Does.Contain("10:00 20/08/2026"));
        Assert.That(ConsultationReminderEmailBuilder.Subject, Is.EqualTo("Nhắc lịch khám MedMateAI"));
    }

    [Test]
    public void BuildHtml_EncodesHtmlInDisplayName()
    {
        var html = ConsultationReminderEmailBuilder.BuildHtml(
            "<script>alert(1)</script>",
            null,
            "Khoa",
            "CSYT",
            null);

        Assert.That(html, Does.Not.Contain("<script>"));
        Assert.That(html, Does.Contain("&lt;script&gt;"));
    }
}
