using System.Globalization;
using Microsoft.Data.Sqlite;

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

    static string VesselClause(long? vesselId) => vesselId == null ? "vessel_id IS NULL" : "vessel_id=$2";

    /// Warnings shown before and after saving (the reading is still saved — corrections are new rows).
    public static List<string> Warnings(SqliteConnection c, Input r)
    {
        var w = new List<string>();
        if (r.Type == "initial_brix")
        {
            // First fermentation reading of this vessel/batch: any non-Brix reading, or a lab row (e.g. the Dataset A3 fermentation log)
            var first = Db.Query(c, $"""
                SELECT MIN(t) t FROM (
                  SELECT taken_at AS t FROM readings WHERE batch_id=$1 AND {VesselClause(r.VesselId)} AND type<>'initial_brix' AND taken_at<=$3
                  UNION ALL SELECT date || 'T09:00' FROM lab WHERE batch_id=$1 AND {VesselClause(r.VesselId)} AND date || 'T09:00'<=$3)
                """, r.BatchId, r.VesselId, r.TakenAt)[0]["t"] as string;
            if (first != null)
                w.Add($"Fermentation has already started in this vessel (first reading {Fmt.DateTime(first)}). Refractometers are not accurate once alcohol is present — use the hydrometer. Initial Brix should be taken before fermentation.");
            if (Db.Query(c, $"SELECT 1 FROM readings WHERE batch_id=$1 AND {VesselClause(r.VesselId)} AND type='initial_brix'", r.BatchId, r.VesselId).Count > 0)
                w.Add("Initial Brix is recorded once per vessel/batch. This entry is saved as a correction — the latest Initial Brix is used for the calculations.");
        }
        if (r.Type == "hydrometer_abv" && LatestBrix(c, r.BatchId, r.VesselId) == null)
            w.Add("No Initial Brix for this vessel/batch yet — fermentation progress cannot be calculated.");
        return w;
    }

    public static double? LatestBrix(SqliteConnection c, long batchId, long? vesselId) =>
        Db.D(Db.Query(c, $"SELECT value FROM readings WHERE batch_id=$1 AND {VesselClause(vesselId)} AND type='initial_brix' ORDER BY id DESC LIMIT 1", batchId, vesselId)
            .FirstOrDefault()?["value"]);

    public static (long id, List<string> warnings) Insert(SqliteConnection c, SqliteTransaction tx, Input r)
    {
        var warnings = Warnings(c, r);
        var id = Db.Insert(c, tx, "INSERT INTO readings(batch_id,vessel_id,taken_at,type,value,unit,instrument,person,note,source) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)",
            r.BatchId, r.VesselId, r.TakenAt, r.Type, r.Value, Units[r.Type], r.Instrument ?? DefaultInstrument[r.Type], r.Person, r.Note, r.Source);
        return (id, warnings);
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
