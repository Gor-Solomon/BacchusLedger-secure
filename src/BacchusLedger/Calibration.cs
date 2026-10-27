using System.Text.Json;
using System.Text.Json.Nodes;

namespace BacchusLedger;

/// Section 5: dip reading (cm from the top rim down to the wine) -> litres.
public static class Calibration
{
    // ---- Cylinder tank ----
    public static double CylinderLitres(double dCm, double hCm) => Math.PI * Math.Pow(dCm / 2, 2) * hCm / 1000.0;

    public static string CylinderJson(double dCm, double hCm) =>
        new JsonObject { ["kind"] = "cylinder", ["d_cm"] = dCm, ["h_cm"] = hCm }.ToJsonString();

    // ---- Qvevri (Dataset A5 generalised profile) ----
    public const double QvevriH = 180, QvevriTarget = 1500, QvevriNeckD = 60, QvevriMaxD = 137.46;

    public static double QvevriRadiusRaw(double z) => ProfileRadius(z, QvevriH, QvevriNeckD / 2, QvevriMaxD / 2);

    /// A5 radius profile. With the A5 defaults (H 180, neck r 30, max r 68.73) this is exactly the section 5 formula;
    /// with a winemaker's own measurements the breakpoints scale with depth and the bottom radius with the max radius.
    public static double ProfileRadius(double z, double h, double rNeck, double rMax)
    {
        double k = h / QvevriH, zMax = 100 * k, zNeck = 165 * k, rBottom = 10 * rMax / 68.73;
        if (z <= zMax) return rBottom + (rMax - rBottom) * Math.Sin(Math.PI / 2 * z / zMax);
        if (z < zNeck) return rNeck + (rMax - rNeck) * Math.Cos(Math.PI / 2 * (z - zMax) / (zNeck - zMax));
        return rNeck;
    }

    /// Heights every whole cm, plus the exact top if the depth is fractional.
    static double[] Heights(double h)
    {
        var zs = Enumerable.Range(0, (int)Math.Floor(h) + 1).Select(i => (double)i).ToList();
        if (h - zs[^1] > 1e-9) zs.Add(h);
        return zs.ToArray();
    }

    /// Cumulative litres at each height, frustum integration.
    static double[] Integrate(double[] zs, Func<double, double> r)
    {
        var cum = new double[zs.Length];
        double v = 0;
        for (int i = 1; i < zs.Length; i++)
        {
            double dz = zs[i] - zs[i - 1], r0 = r(zs[i - 1]), r1 = r(zs[i]);
            v += Math.PI * dz / 3.0 * (r0 * r0 + r0 * r1 + r1 * r1);
            cum[i] = v / 1000.0;
        }
        return cum;
    }

    public static double QvevriRawTotal(double h = QvevriH, double neckD = QvevriNeckD, double maxD = QvevriMaxD) =>
        Integrate(Heights(h), z => ProfileRadius(z, h, neckD / 2, maxD / 2))[^1];

    /// Calibration table. targetL = known volume (radii scaled so the full volume matches) or null (shape volume as measured).
    public static string QvevriJson(double h = QvevriH, double neckD = QvevriNeckD, double maxD = QvevriMaxD, double? targetL = QvevriTarget, string model = "A5")
    {
        double raw = QvevriRawTotal(h, neckD, maxD);
        double scale = targetL is > 0 ? Math.Sqrt(targetL.Value / raw) : 1;
        var zs = Heights(h);
        Func<double, double> r = z => ProfileRadius(z, h, neckD / 2, maxD / 2) * scale;
        var cum = Integrate(zs, r);
        var table = new JsonArray();
        for (int i = 0; i < zs.Length; i++)
            table.Add(new JsonArray(Math.Round(zs[i], 3), Math.Round(r(zs[i]), 3), Math.Round(cum[i], 3)));
        return new JsonObject
        {
            ["kind"] = "qvevri", ["model"] = model, ["h_cm"] = h, ["raw_total_l"] = Math.Round(raw, 2), ["scale"] = Math.Round(scale, 6),
            ["table"] = table, // [z_cm, radius_cm, cumulative_litres]
        }.ToJsonString();
    }

    // ---- Generic API over calibration_json ----
    public sealed class Model
    {
        public string Kind = "";
        public double H, D;
        public double[] Z = [], Cum = [];

        public double Capacity => Kind == "cylinder" ? CylinderLitres(D, H) : Cum[^1];

        /// Litres for a fill height in cm.
        public double LitresAtHeight(double h)
        {
            h = Math.Clamp(h, 0, H);
            if (Kind == "cylinder") return CylinderLitres(D, h);
            for (int i = 1; i < Z.Length; i++)
                if (Z[i] >= h) return Cum[i - 1] + (Cum[i] - Cum[i - 1]) * ((h - Z[i - 1]) / (Z[i] - Z[i - 1]));
            return Cum[^1];
        }

        public double LitresAtDip(double dipCm) => LitresAtHeight(H - dipCm);

        /// Inverse: dip (cm from rim) for a given volume.
        public double DipForLitres(double litres)
        {
            litres = Math.Clamp(litres, 0, Capacity);
            if (Kind == "cylinder") return H - litres * 1000.0 / (Math.PI * Math.Pow(D / 2, 2));
            for (int i = 1; i < Cum.Length; i++)
                if (Cum[i] >= litres)
                {
                    double frac = (litres - Cum[i - 1]) / (Cum[i] - Cum[i - 1]);
                    return H - (Z[i - 1] + frac * (Z[i] - Z[i - 1]));
                }
            return 0;
        }
    }

    /// Null when the vessel has no calibration ("Volume unknown — enter litres manually").
    public static Model? TryParse(string? json) => string.IsNullOrEmpty(json) ? null : Parse(json);

    public static Model Parse(string json)
    {
        var n = JsonNode.Parse(json)!;
        var m = new Model { Kind = (string)n["kind"]!, H = (double)n["h_cm"]! };
        if (m.Kind == "cylinder") m.D = (double)n["d_cm"]!;
        else
        {
            var t = n["table"]!.AsArray();
            m.Z = t.Select(r => (double)r![0]!).ToArray();
            m.Cum = t.Select(r => (double)r![2]!).ToArray();
        }
        return m;
    }

    /// Printable dip chart every `step` cm.
    public static List<object> DipChart(Model m, int step = 5)
    {
        var rows = new List<object>();
        for (int u = 0; u <= (int)m.H; u += step)
            rows.Add(new { dip_cm = u, fill_cm = m.H - u, litres = Math.Round(m.LitresAtDip(u), 1) });
        return rows;
    }
}
