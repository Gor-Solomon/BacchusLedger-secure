using System.Globalization;

namespace BacchusLedger.Domain.Readings;

/// Refuses typing errors (unknown type, bad time, physically impossible value); values are never corrected.
public sealed class ReadingValidator
{
    static readonly Dictionary<string, (double Min, double Max)> Range = new()
    {
        ["initial_brix"] = (0, 45), ["ph"] = (0, 14), ["temperature"] = (-10, 50), ["hydrometer_density"] = (0.9, 1.25), ["hydrometer_abv"] = (0, 25), ["ta"] = (0, 30),
    };

    /// The error text, or null when the reading is valid.
    public string? Validate(ReadingInput r)
    {
        if (!ReadingTypes.All.Contains(r.Type)) return "Unknown reading type";
        if (!DateTime.TryParseExact(r.TakenAt, ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return "Date/time must be YYYY-MM-DDTHH:MM";
        var (min, max) = Range[r.Type];
        if (double.IsNaN(r.Value) || r.Value < min || r.Value > max) return $"{r.Type} must be between {min} and {max} {ReadingTypes.Units[r.Type]}";
        return null;
    }
}
