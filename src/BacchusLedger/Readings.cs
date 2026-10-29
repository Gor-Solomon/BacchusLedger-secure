using System.Globalization;

namespace BacchusLedger;

/// Update 2: manual fermentation readings (timestamped, attached to a vessel + its batch). Append-only.
public static class Readings
{
    public static readonly string[] Types = ["initial_brix", "ph", "temperature", "hydrometer_density", "hydrometer_abv", "ta"];

    public static readonly Dictionary<string, string> Units = new()
    {
        ["initial_brix"] = "°Bx", ["ph"] = "pH", ["temperature"] = "°C", ["hydrometer_density"] = "SG", ["hydrometer_abv"] = "% vol", ["ta"] = "g/L",
    };

    public static readonly Dictionary<string, string> DefaultInstrument = new()
    {
        ["initial_brix"] = "refractometer", ["ph"] = "pH meter / lab", ["temperature"] = "thermometer / probe",
        ["hydrometer_density"] = "hydrometer", ["hydrometer_abv"] = "hydrometer", ["ta"] = "titration",
    };

    /// Plausible physical range per type — values outside are refused (typing errors), not corrected.
    static readonly Dictionary<string, (double Min, double Max)> Range = new()
    {
        ["initial_brix"] = (0, 45), ["ph"] = (0, 14), ["temperature"] = (-10, 50), ["hydrometer_density"] = (0.9, 1.25), ["hydrometer_abv"] = (0, 25), ["ta"] = (0, 30),
    };

    // ---- Calculations (section 4) ----
    /// potential_abv ≈ initial_brix × 0.59 (rule of thumb).
    public static double PotentialAbv(double brix) => Math.Round(brix * 0.59, 2, MidpointRounding.AwayFromZero);

    /// SG = 1 + brix / (258.6 − (brix / 258.2) × 227.1)
    public static double BrixToSg(double brix) => 1 + brix / (258.6 - brix / 258.2 * 227.1);

    public static double ProgressPct(double abvReached, double potential) => Math.Round(abvReached / potential * 100, 1, MidpointRounding.AwayFromZero);

    public const string FormulaPotential = "potential ABV ≈ Initial Brix × 0.59 (rule of thumb, approx.)";
    public const string FormulaProgress = "progress = ABV reached (hydrometer) ÷ potential ABV × 100%";
    public const string FormulaSg = "SG = 1 + Brix ÷ (258.6 − (Brix ÷ 258.2) × 227.1)";

    public record Input(long BatchId, long? VesselId, string TakenAt, string Type, double Value, string? Instrument, string? Person, string? Note, string Source);

    public static string? Validate(Input r)
    {
        if (!Types.Contains(r.Type)) return "Unknown reading type";
        if (!DateTime.TryParseExact(r.TakenAt, ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return "Date/time must be YYYY-MM-DDTHH:MM";
        var (min, max) = Range[r.Type];
        if (double.IsNaN(r.Value) || r.Value < min || r.Value > max) return $"{r.Type} must be between {min} and {max} {Units[r.Type]}";
        return null;
    }

    /// Calculated values for one reading given the Initial Brix of its vessel/batch (all null when nothing applies).
    public static CalcResult Calc(string type, double value, double? brix) => type switch
    {
        "initial_brix" => new(PotentialAbv(value), Math.Round(BrixToSg(value), 4), null, FormulaPotential + " · " + FormulaSg),
        "hydrometer_abv" when brix is { } b && PotentialAbv(b) > 0 =>
            new(PotentialAbv(b), null, ProgressPct(value, PotentialAbv(b)), FormulaProgress + " · " + FormulaPotential),
        _ => new(null, null, null, null),
    };

    public sealed record CalcResult(double? PotentialAbv, double? Sg, double? ProgressPct, string? Formula);
}
