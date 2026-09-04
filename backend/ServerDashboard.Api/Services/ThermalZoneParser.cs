namespace ServerDashboard.Api.Services;

// Pure parsing/selection logic for Linux's /sys/class/thermal/thermal_zone*
// sysfs interface — no file I/O, so it's unit testable on any OS. A system
// typically exposes several zones (Wi-Fi, ACPI, individual cores, the package
// as a whole, ...); we prefer a zone whose "type" file names it as the CPU
// package, and fall back to whatever the first zone reports otherwise.
public static class ThermalZoneParser
{
    public readonly record struct ThermalZone(string Type, string TempFileContents);

    // Zone "type" values seen across common CPUs (Intel/AMD desktops and
    // servers, and ARM boards like the Raspberry Pi).
    private static readonly string[] KnownCpuZoneTypes =
    [
        "x86_pkg_temp",
        "cpu_thermal",
        "cpu-thermal",
        "soc_thermal",
        "acpitz",
    ];

    public static double? SelectCpuTemperatureCelsius(IReadOnlyList<ThermalZone> zones)
    {
        if (zones.Count == 0)
        {
            return null;
        }

        var cpuZone = zones.FirstOrDefault(
            z => KnownCpuZoneTypes.Contains(z.Type, StringComparer.OrdinalIgnoreCase));
        var chosen = cpuZone.Type is not null ? cpuZone : zones[0];

        return long.Parse(chosen.TempFileContents.Trim()) / 1000.0;
    }
}
