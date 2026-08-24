using ServerDashboard.Api.Services;
using Xunit;

namespace ServerDashboard.Api.Tests;

public class MemInfoParserTests
{
    [Fact]
    public void Parse_ReadsMemTotalAndMemAvailable_IgnoringOtherLines()
    {
        const string contents =
            "MemTotal:       16384000 kB\n" +
            "MemFree:         2000000 kB\n" +
            "MemAvailable:    8192000 kB\n" +
            "Buffers:          500000 kB\n";

        var result = MemInfoParser.Parse(contents);

        Assert.Equal(16384000, result.TotalKb);
        Assert.Equal(8192000, result.AvailableKb);
    }
}
