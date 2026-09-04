namespace ServerDashboard.Api.Services;

// Pure parsing for /proc/uptime — no file I/O, unit testable on any OS.
public static class UptimeParser
{
    public static double Parse(string uptimeContents)
    {
        // e.g. "12345.67 6789.01" -> seconds since boot is the first field.
        var firstToken = uptimeContents.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return double.Parse(firstToken);
    }
}
