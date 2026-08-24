using ServerDashboard.Api.Services;
using Xunit;

namespace ServerDashboard.Api.Tests;

public class NvidiaSmiParserTests
{
    [Fact]
    public void ParseFirstGpuLine_SingleGpu_ReadsAllFields()
    {
        const string csvOutput = "45, 2048, 8192, 65\n";

        var reading = NvidiaSmiParser.ParseFirstGpuLine(csvOutput);

        Assert.Equal(45, reading.UtilizationPercent);
        Assert.Equal(2048, reading.MemoryUsedMb);
        Assert.Equal(8192, reading.MemoryTotalMb);
        Assert.Equal(65, reading.TemperatureCelsius);
    }

    [Fact]
    public void ParseFirstGpuLine_MultipleGpus_UsesOnlyFirstLine()
    {
        const string csvOutput = "45, 2048, 8192, 65\n10, 512, 8192, 40\n";

        var reading = NvidiaSmiParser.ParseFirstGpuLine(csvOutput);

        Assert.Equal(45, reading.UtilizationPercent);
    }
}
