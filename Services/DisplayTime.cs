using System.Globalization;

namespace SecureGate1.Services;

public static class DisplayTime
{
    public const string LocalFormat = "MMM d, yyyy 'at' h:mm tt";

    public static string Local(DateTime utc) =>
        utc.ToLocalTime().ToString(LocalFormat, CultureInfo.InvariantCulture);

    public static string Local(DateTime? utc) =>
        utc is null ? "—" : Local(utc.Value);
}
