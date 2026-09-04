using ServerDashboard.Api.Services;
using Xunit;

namespace ServerDashboard.Api.Tests;

public class SensorsJsonParserTests
{
    [Fact]
    public void SelectCpuTemperatureCelsius_K10TempChip_ReadsTctlSensor()
    {
        // Actual `sensors -j` output from an AMD (k10temp) home server.
        const string json = """{"k10temp-pci-00c3":{"Adapter":"PCI adapter","Tctl":{"temp1_input":27.750000}}}""";

        var temp = SensorsJsonParser.SelectCpuTemperatureCelsius(json);

        Assert.Equal(27.75, temp);
    }

    [Fact]
    public void SelectCpuTemperatureCelsius_CoretempChip_PrefersPackageSensorOverPerCoreSensors()
    {
        const string json = """
            {
                "coretemp-isa-0000": {
                    "Adapter": "ISA adapter",
                    "Package id 0": { "temp1_input": 45.0 },
                    "Core 0": { "temp2_input": 40.0 },
                    "Core 1": { "temp3_input": 41.0 }
                }
            }
            """;

        var temp = SensorsJsonParser.SelectCpuTemperatureCelsius(json);

        Assert.Equal(45.0, temp);
    }

    [Fact]
    public void SelectCpuTemperatureCelsius_MultipleChips_PrefersKnownCpuChipRegardlessOfOrder()
    {
        const string json = """
            {
                "nvme-pci-0100": { "Adapter": "PCI adapter", "Composite": { "temp1_input": 35.0 } },
                "k10temp-pci-00c3": { "Adapter": "PCI adapter", "Tctl": { "temp1_input": 52.5 } }
            }
            """;

        var temp = SensorsJsonParser.SelectCpuTemperatureCelsius(json);

        Assert.Equal(52.5, temp);
    }

    [Fact]
    public void SelectCpuTemperatureCelsius_NoKnownChipOrSensor_FallsBackToFirstOfEach()
    {
        const string json = """{"some_unknown_chip":{"Adapter":"ISA adapter","some_unknown_sensor":{"temp1_input":30.0}}}""";

        var temp = SensorsJsonParser.SelectCpuTemperatureCelsius(json);

        Assert.Equal(30.0, temp);
    }

    [Fact]
    public void SelectCpuTemperatureCelsius_NoChipsReported_ReturnsNull()
    {
        var temp = SensorsJsonParser.SelectCpuTemperatureCelsius("{}");

        Assert.Null(temp);
    }
}
