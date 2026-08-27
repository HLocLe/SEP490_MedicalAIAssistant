using MedMateAI.Application.Common.Time;
using NUnit.Framework;

namespace MedMateAI.Tests.Common;

[TestFixture]
public class VietnamBusinessDateTests
{
    [Test]
    [Category("N")]
    public void ConvertVietnamLocalToUtc_UnspecifiedNineAm_IsTwoAmUtc()
    {
        var vietnamLocal = new DateTime(2026, 3, 15, 9, 0, 0, DateTimeKind.Unspecified);

        var utc = VietnamBusinessDate.ConvertVietnamLocalToUtc(vietnamLocal);

        Assert.That(utc.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(utc, Is.EqualTo(new DateTime(2026, 3, 15, 2, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    [Category("N")]
    public void ConvertUtcToVietnamLocal_TwoAmUtc_IsNineAmVietnam()
    {
        var utc = new DateTime(2026, 3, 15, 2, 0, 0, DateTimeKind.Utc);

        var vietnamLocal = VietnamBusinessDate.ConvertUtcToVietnamLocal(utc);

        Assert.That(vietnamLocal, Is.EqualTo(new DateTime(2026, 3, 15, 9, 0, 0)));
    }
}
