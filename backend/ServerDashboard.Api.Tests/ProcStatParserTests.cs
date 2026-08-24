using ServerDashboard.Api.Services;
using Xunit;

namespace ServerDashboard.Api.Tests;

public class ProcStatParserTests
{
    [Fact]
    public void ParseAggregateCpuLine_UsesFirstCpuLine_NotPerCoreLines()
    {
        // Real /proc/stat has a "cpu " aggregate line, then "cpu0 ", "cpu1 ",
        // etc. per-core lines. We only want the aggregate one.
        const string contents =
            "cpu  100 0 100 700 0 0 0 0 0 0\n" +
            "cpu0 50 0 50 350 0 0 0 0 0 0\n" +
            "cpu1 50 0 50 350 0 0 0 0 0 0\n" +
            "intr 12345 0 0 0\n";

        var result = ProcStatParser.ParseAggregateCpuLine(contents);

        Assert.Equal(700, result.Idle); // idle(700) + iowait(0)
        Assert.Equal(900, result.Total); // sum of all 10 fields
    }

    [Fact]
    public void CalculateUsagePercent_TwoThirdsBusy_Returns66Point67()
    {
        var before = new ProcStatParser.CpuTimes(Idle: 700, Total: 900);
        var after = new ProcStatParser.CpuTimes(Idle: 750, Total: 1050);
        // deltaTotal = 150, deltaIdle = 50 -> 1 - 50/150 = 66.67% busy

        var usage = ProcStatParser.CalculateUsagePercent(before, after);

        Assert.Equal(66.6667, usage, precision: 3);
    }

    [Fact]
    public void CalculateUsagePercent_NoTimeElapsed_ReturnsZeroInsteadOfDividingByZero()
    {
        var sample = new ProcStatParser.CpuTimes(Idle: 700, Total: 900);

        var usage = ProcStatParser.CalculateUsagePercent(sample, sample);

        Assert.Equal(0, usage);
    }
}
