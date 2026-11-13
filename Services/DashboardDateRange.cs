using System.Globalization;

namespace SecureGate1.Services;

public static class DashboardPresets
{
    public const string Today = "today";
    public const string Last7Days = "7d";
    public const string LastMonth = "month";
    public const string LastYear = "year";
    public const string Custom = "custom";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Today, Last7Days, LastMonth, LastYear, Custom
    };
}

public sealed class DashboardRange
{
    public string Preset { get; init; } = DashboardPresets.Last7Days;
    public DateTime FromUtc { get; init; }
    public DateTime ToUtcExclusive { get; init; }
    public DateOnly FromLocal { get; init; }
    public DateOnly ToLocal { get; init; }
    public string Label { get; init; } = "";
}

public static class DashboardDateRange
{
    public static DashboardRange? Resolve(
        string? preset,
        DateOnly? fromLocal,
        DateOnly? toLocal,
        TimeProvider time,
        TimeZoneInfo timeZone,
        int maxRangeDays,
        out string? error)
    {
        error = null;
        var key = string.IsNullOrWhiteSpace(preset) ? DashboardPresets.Last7Days : preset.Trim();
        if (!DashboardPresets.All.Contains(key))
        {
            error = "Choose a valid date range.";
            return null;
        }

        var today = LocalDate(time.GetUtcNow().UtcDateTime, timeZone);

        DateOnly start;
        DateOnly end;
        if (string.Equals(key, DashboardPresets.Custom, StringComparison.OrdinalIgnoreCase))
        {
            if (fromLocal is null || toLocal is null)
            {
                error = "Choose a start date and an end date.";
                return null;
            }

            start = fromLocal.Value;
            end = toLocal.Value;
        }
        else if (string.Equals(key, DashboardPresets.Today, StringComparison.OrdinalIgnoreCase))
        {
            start = today;
            end = today;
        }
        else if (string.Equals(key, DashboardPresets.Last7Days, StringComparison.OrdinalIgnoreCase))
        {
            start = today.AddDays(-6);
            end = today;
        }
        else if (string.Equals(key, DashboardPresets.LastMonth, StringComparison.OrdinalIgnoreCase))
        {
            start = today.AddDays(-29);
            end = today;
        }
        else
        {
            start = today.AddYears(-1);
            end = today;
        }

        if (end < start)
        {
            error = "The end date must be on or after the start date.";
            return null;
        }

        var fromUtc = ToUtc(start, timeZone);
        var toUtcExclusive = ToUtc(end.AddDays(1), timeZone);
        var days = (toUtcExclusive - fromUtc).TotalDays;
        if (days > maxRangeDays)
        {
            error = $"Choose a range of {maxRangeDays} days or fewer.";
            return null;
        }

        return new DashboardRange
        {
            Preset = string.Equals(key, DashboardPresets.Custom, StringComparison.OrdinalIgnoreCase)
                ? DashboardPresets.Custom
                : key.ToLowerInvariant(),
            FromUtc = fromUtc,
            ToUtcExclusive = toUtcExclusive,
            FromLocal = start,
            ToLocal = end,
            Label = FormatLabel(key, start, end)
        };
    }

    public static DateOnly LocalDate(DateTime utc, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone);
        return DateOnly.FromDateTime(local);
    }

    public static DateTime ToUtc(DateOnly localDate, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone);
    }

    private static string FormatLabel(string preset, DateOnly start, DateOnly end)
    {
        var from = start.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
        var to = end.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
        var span = start == end ? from : $"{from} – {to}";
        var name = preset.ToLowerInvariant() switch
        {
            DashboardPresets.Today => "Today",
            DashboardPresets.Last7Days => "Last 7 days",
            DashboardPresets.LastMonth => "Last month",
            DashboardPresets.LastYear => "Last year",
            _ => "Custom range"
        };
        return $"{name} · {span}";
    }
}
