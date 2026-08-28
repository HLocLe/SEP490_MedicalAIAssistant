namespace MedMateAI.Application.Helpers.GeoDistance;

public static class GeoDistanceHelper
{
    private const double EarthRadiusKm = 6371.0;

    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

        var deltaLat = ToRadians(lat2 - lat1);
        var deltaLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2)
                + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
                * Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    public static (double MinLat, double MaxLat, double MinLon, double MaxLon) GetBoundingBox(
        double latitude,
        double longitude,
        double radiusKm)
    {
        var latDelta = radiusKm / 111.0;
        var lonDelta = radiusKm / (111.0 * Math.Cos(latitude * Math.PI / 180.0));
        return (
            latitude - latDelta,
            latitude + latDelta,
            longitude - lonDelta,
            longitude + lonDelta);
    }
}
