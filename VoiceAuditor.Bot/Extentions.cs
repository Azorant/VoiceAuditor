namespace VoiceAuditor.Bot;

public static class Extentions
{
    public static string Format(this TimeSpan time)
    {
        List<string> parts = [];
        if (time.Days > 0) parts.Add($"{time.Days} day{Plural(time.Days)}");
        if (time.Hours > 0) parts.Add($"{time.Hours} hour{Plural(time.Hours)}");
        if (time.Minutes > 0) parts.Add($"{time.Minutes} minute{Plural(time.Minutes)}");
        if (time.Seconds > 0 || parts.Count == 0) parts.Add($"{time.Seconds} second{Plural(time.Seconds)}");
        return string.Join(' ', parts);
    }
    
    private static string Plural(int number)
    {
        return number is > 1 or 0 ? "s" : string.Empty;
    }
    
    public static DateTime Stupid(this DateTime date)
    {
        return DateTime.SpecifyKind(date, DateTimeKind.Utc);
    }
}