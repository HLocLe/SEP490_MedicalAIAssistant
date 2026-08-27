namespace MedMateAI.Application.Common.Time;

public static class VietnamBusinessDate
{
    public const string IanaTimeZoneId = "Asia/Ho_Chi_Minh";
    public const string WindowsTimeZoneId = "SE Asia Standard Time";

    private static readonly Lazy<TimeZoneInfo> VietnamTimeZone =
        new(ResolveVietnamTimeZone);

    public static TimeZoneInfo TimeZone => VietnamTimeZone.Value;

    public static DateOnly GetToday(DateTimeOffset utcNow)
    {
        var vietnamNow = TimeZoneInfo.ConvertTime(
            utcNow.ToUniversalTime(),
            VietnamTimeZone.Value);

        return DateOnly.FromDateTime(vietnamNow.DateTime);
    }

    public static DateTime ConvertVietnamLocalToUtc(DateTime vietnamLocal)
    {
        var local = DateTime.SpecifyKind(vietnamLocal, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, VietnamTimeZone.Value);
    }

    public static DateTime ConvertUtcToVietnamLocal(DateTime utc)
    {
        var asUtc = utc.Kind == DateTimeKind.Utc
            ? utc
            : DateTime.SpecifyKind(utc.ToUniversalTime(), DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(asUtc, VietnamTimeZone.Value);
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        var timeZone = TryFindTimeZone(IanaTimeZoneId)
            ?? TryFindTimeZone(WindowsTimeZoneId);

        return timeZone
            ?? throw new TimeZoneNotFoundException(
                $"Neither '{IanaTimeZoneId}' nor '{WindowsTimeZoneId}' is available.");
    }

    private static TimeZoneInfo? TryFindTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }
}
