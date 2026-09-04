using System.Text.Json;

namespace ServerDashboard.Api.Services;

// Pure parsing/selection for `sensors -j` (lm-sensors) output — no process
// launching, so it's unit testable on any OS. Shape looks like:
// { "<chip>": { "Adapter": "...", "<sensor label>": { "tempN_input": 27.75, ... }, ... }, ... }
// A system reports many chips (NVMe, motherboard, CPU...), and a CPU chip
// itself reports several sensors (die temp, control temp, per-core temps...),
// so we prefer known CPU chip/sensor names and fall back to whatever's first.
public static class SensorsJsonParser
{
    // Chip name prefixes for the CPU temperature driver, not motherboard/NVMe/etc.
    // "k10temp" = AMD, "coretemp" = Intel.
    private static readonly string[] KnownCpuChipPrefixes = ["k10temp", "coretemp"];

    // Sensor labels representing the whole-package/control temperature, not an
    // individual core or chiplet.
    private static readonly string[] KnownCpuSensorLabels = ["Tctl", "Tdie", "Package id 0", "Physical id 0"];

    public static double? SelectCpuTemperatureCelsius(string sensorsJsonOutput)
    {
        using var doc = JsonDocument.Parse(sensorsJsonOutput);
        var chips = doc.RootElement.EnumerateObject().ToList();
        if (chips.Count == 0)
        {
            return null;
        }

        var chip = FindFirst(chips, name => KnownCpuChipPrefixes.Any(
            prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) ?? chips[0].Value;

        return SelectTemperatureFromChip(chip);
    }

    private static double? SelectTemperatureFromChip(JsonElement chip)
    {
        var sensors = chip.EnumerateObject().Where(p => p.Name != "Adapter").ToList();
        if (sensors.Count == 0)
        {
            return null;
        }

        var sensor = FindFirst(sensors, name => KnownCpuSensorLabels.Contains(
            name, StringComparer.OrdinalIgnoreCase)) ?? sensors[0].Value;

        foreach (var field in sensor.EnumerateObject())
        {
            if (field.Name.EndsWith("_input", StringComparison.Ordinal))
            {
                return field.Value.GetDouble();
            }
        }

        return null;
    }

    private static JsonElement? FindFirst(List<JsonProperty> properties, Func<string, bool> namePredicate)
    {
        foreach (var property in properties)
        {
            if (namePredicate(property.Name))
            {
                return property.Value;
            }
        }

        return null;
    }
}
