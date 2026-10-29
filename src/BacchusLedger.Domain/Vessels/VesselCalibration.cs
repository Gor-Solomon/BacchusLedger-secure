using System.Text.Json.Nodes;

namespace BacchusLedger.Domain.Vessels;

/// Dip reading (cm from the top rim down to the wine) ↔ litres, from a vessel's calibration_json.
public sealed class VesselCalibration
{
    readonly double[] z = [], cum = [];

    VesselCalibration(string kind, double heightCm, double diameterCm, double[] z, double[] cum)
    {
        Kind = kind; HeightCm = heightCm; DiameterCm = diameterCm; this.z = z; this.cum = cum;
    }

    /// "cylinder" or "qvevri".
    public string Kind { get; }
    public double HeightCm { get; }

    /// Cylinders only.
    public double DiameterCm { get; }

    public double Capacity => Kind == "cylinder" ? new CylinderTank(DiameterCm, HeightCm).Capacity : cum[^1];

    /// Null when the vessel has no calibration ("Volume unknown: enter litres manually").
    public static VesselCalibration? TryParse(string? json) => string.IsNullOrEmpty(json) ? null : Parse(json);

    public static VesselCalibration Parse(string json)
    {
        var n = JsonNode.Parse(json)!;
        var kind = (string)n["kind"]!;
        var h = (double)n["h_cm"]!;
        if (kind == "cylinder") return new(kind, h, (double)n["d_cm"]!, [], []);
        var t = n["table"]!.AsArray();
        return new(kind, h, 0, t.Select(r => (double)r![0]!).ToArray(), t.Select(r => (double)r![2]!).ToArray());
    }

    /// Litres for a fill height in cm.
    public double LitresAtHeight(double h)
    {
        h = Math.Clamp(h, 0, HeightCm);
        if (Kind == "cylinder") return new CylinderTank(DiameterCm, HeightCm).LitresAtHeight(h);
        for (int i = 1; i < z.Length; i++)
            if (z[i] >= h) return cum[i - 1] + (cum[i] - cum[i - 1]) * ((h - z[i - 1]) / (z[i] - z[i - 1]));
        return cum[^1];
    }

    public double LitresAtDip(double dipCm) => LitresAtHeight(HeightCm - dipCm);

    /// Inverse: dip (cm from rim) for a given volume.
    public double DipForLitres(double litres)
    {
        litres = Math.Clamp(litres, 0, Capacity);
        if (Kind == "cylinder") return HeightCm - new CylinderTank(DiameterCm, HeightCm).HeightForLitres(litres);
        for (int i = 1; i < cum.Length; i++)
            if (cum[i] >= litres)
            {
                double frac = (litres - cum[i - 1]) / (cum[i] - cum[i - 1]);
                return HeightCm - (z[i - 1] + frac * (z[i] - z[i - 1]));
            }
        return 0;
    }

    /// Printable dip chart every step cm.
    public List<DipChartPoint> DipChart(int step = 5)
    {
        var rows = new List<DipChartPoint>();
        for (int u = 0; u <= (int)HeightCm; u += step)
            rows.Add(new(u, HeightCm - u, Math.Round(LitresAtDip(u), 1)));
        return rows;
    }
}
