using ServerDashboard.Api.Services;
using Xunit;

namespace ServerDashboard.Api.Tests;

public class UptimeParserTests
{
    [Fact]
    public void Parse_ReadsFirstField_AsUptimeSeconds()
    {
        // "/proc/uptime" is "<seconds since boot> <seconds idle, summed across cores>"
        const string contents = "12345.67 6789.01\n";

        double result = UptimeParser.Parse(contents);

        Assert.Equal(12345.67, result, precision: 2);
    }

    [Fact]
    public void Parse_IgnoresTrailingWhitespace()
    {
        const string contents = "42.00 0.00\n";

        double result = UptimeParser.Parse(contents);

        Assert.Equal(42.00, result, precision: 2);
    }
}
