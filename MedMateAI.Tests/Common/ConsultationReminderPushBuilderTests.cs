using MedMateAI.Application.Common;
using NUnit.Framework;

namespace MedMateAI.Tests.Common;

[TestFixture]
public class ConsultationReminderPushBuilderTests
{
    [Test]
    [Category("N")]
    public void BuildData_IncludesConsultationSessionReference()
    {
        var sessionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var data = ConsultationReminderPushBuilder.BuildData(sessionId);

        Assert.Multiple(() =>
        {
            Assert.That(data["notificationType"], Is.EqualTo(NotificationTypes.ConsultationReminder));
            Assert.That(data["referenceType"], Is.EqualTo(NotificationReferenceTypes.ConsultationSession));
            Assert.That(data["referenceId"], Is.EqualTo(sessionId.ToString("D")));
        });
    }

    [Test]
    [Category("N")]
    public void BuildBody_IncludesDepartmentFacilityAndAppointment()
    {
        var appointmentUtc = new DateTime(2026, 3, 15, 2, 0, 0, DateTimeKind.Utc);

        var body = ConsultationReminderPushBuilder.BuildBody(
            "Nội",
            "BV A",
            appointmentUtc);

        Assert.That(body, Does.Contain("Khoa Nội"));
        Assert.That(body, Does.Contain("BV A"));
        Assert.That(body, Does.Contain("09:00 15/03/2026"));
    }
}
