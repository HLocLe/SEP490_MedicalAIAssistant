using MedMateAI.Application.Helpers.GeoDistance;
using NUnit.Framework;

namespace MedMateAI.Tests.Helpers;

[TestFixture]
public class GeoDistanceHelperTests
{
    [Test]
    [Category("N")]
    public void DistanceKm_SamePoint_IsZero()
    {
        var distance = GeoDistanceHelper.DistanceKm(10.7769, 106.7009, 10.7769, 106.7009);

        Assert.That(distance, Is.EqualTo(0).Within(0.001));
    }

    [Test]
    [Category("N")]
    public void GetBoundingBox_ContainsOriginalPoint()
    {
        var (minLat, maxLat, minLon, maxLon) = GeoDistanceHelper.GetBoundingBox(10.7769, 106.7009, 5);

        Assert.Multiple(() =>
        {
            Assert.That(minLat, Is.LessThan(10.7769));
            Assert.That(maxLat, Is.GreaterThan(10.7769));
            Assert.That(minLon, Is.LessThan(106.7009));
            Assert.That(maxLon, Is.GreaterThan(106.7009));
        });
    }
}
