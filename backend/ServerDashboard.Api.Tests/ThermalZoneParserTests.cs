using ServerDashboard.Api.Services;
using Xunit;

namespace ServerDashboard.Api.Tests;

public class ThermalZoneParserTests
{
    [Fact]
    public void SelectCpuTemperatureCelsius_KnownCpuZonePresent_PrefersItOverOtherZones()
    {
        var zones = new[]
        {
            new ThermalZoneParser.ThermalZone("iwlwifi_1", "38000"),
            new ThermalZoneParser.ThermalZone("x86_pkg_temp", "52500"),
        };

        var temp = ThermalZoneParser.SelectCpuTemperatureCelsius(zones);

        Assert.Equal(52.5, temp);
    }

    [Fact]
    public void SelectCpuTemperatureCelsius_NoKnownCpuZoneType_FallsBackToFirstZone()
    {
        var zones = new[]
        {
            new ThermalZoneParser.ThermalZone("some_unrecognized_zone", "40000"),
        };

        var temp = ThermalZoneParser.SelectCpuTemperatureCelsius(zones);

        Assert.Equal(40.0, temp);
    }

    [Fact]
    public void SelectCpuTemperatureCelsius_NoZones_ReturnsNull()
    {
        var temp = ThermalZoneParser.SelectCpuTemperatureCelsius(
            Array.Empty<ThermalZoneParser.ThermalZone>());

        Assert.Null(temp);
    }

    [Fact]
    public void SelectCpuTemperatureCelsius_ConvertsMilliCelsiusToCelsius()
    {
        // /sys/class/thermal/thermal_zone*/temp reports milli-degrees C,
        // often with a trailing newline.
        var zones = new[] { new ThermalZoneParser.ThermalZone("x86_pkg_temp", "45500\n") };

        var temp = ThermalZoneParser.SelectCpuTemperatureCelsius(zones);

        Assert.Equal(45.5, temp);
    }
}
