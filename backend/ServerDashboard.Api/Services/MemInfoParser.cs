namespace ServerDashboard.Api.Services;

// Pure parsing for /proc/meminfo — no file I/O, unit testable on any OS.
public static class MemInfoParser
{
    public readonly record struct MemInfo(long TotalKb, long AvailableKb);

    public static MemInfo Parse(string memInfoContents)
    {
        long totalKb = 0;
        long availableKb = 0;

        foreach (var line in memInfoContents.Split('\n'))
        {
            if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
            {
                totalKb = ExtractKb(line);
            }
            else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
            {
                availableKb = ExtractKb(line);
            }
        }

        return new MemInfo(totalKb, availableKb);
    }

    private static long ExtractKb(string line)
    {
        // e.g. "MemTotal:       16384000 kB" -> the numeric field before "kB"
        var afterColon = line.Split(':', 2)[1];
        var firstToken = afterColon.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return long.Parse(firstToken);
    }
}
