namespace BacchusLedger.Domain.Readings;

/// The manual fermentation reading types, their units and default instruments.
public static class ReadingTypes
{
    public static readonly string[] All = ["initial_brix", "ph", "temperature", "hydrometer_density", "hydrometer_abv", "ta"];

    public static readonly IReadOnlyDictionary<string, string> Units = new Dictionary<string, string>
    {
        ["initial_brix"] = "°Bx", ["ph"] = "pH", ["temperature"] = "°C", ["hydrometer_density"] = "SG", ["hydrometer_abv"] = "% vol", ["ta"] = "g/L",
    };

    public static readonly IReadOnlyDictionary<string, string> DefaultInstrument = new Dictionary<string, string>
    {
        ["initial_brix"] = "refractometer", ["ph"] = "pH meter / lab", ["temperature"] = "thermometer / probe",
        ["hydrometer_density"] = "hydrometer", ["hydrometer_abv"] = "hydrometer", ["ta"] = "titration",
    };

    public const string FormulaPotential = "potential ABV ≈ Initial Brix × 0.59 (rule of thumb, approx.)";
    public const string FormulaProgress = "progress = ABV reached (hydrometer) ÷ potential ABV × 100%";
    public const string FormulaSg = "SG = 1 + Brix ÷ (258.6 − (Brix ÷ 258.2) × 227.1)";
}
