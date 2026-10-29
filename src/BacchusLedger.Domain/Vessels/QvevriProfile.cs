using System.Text.Json.Nodes;

namespace BacchusLedger.Domain.Vessels;

/// Section 5: the qvevri shape (Dataset A5 generalised profile). With the A5 defaults (H 180, neck Ø 60, max Ø 137.46) this is
/// exactly the section 5 formula; with a winemaker's own measurements the breakpoints scale with the depth and the bottom radius with the max radius.
public sealed class QvevriProfile(double heightCm = QvevriProfile.A5Height, double neckDiameterCm = QvevriProfile.A5NeckDiameter, double maxDiameterCm = QvevriProfile.A5MaxDiameter)
{
    public const double A5Height = 180, A5TargetLitres = 1500, A5NeckDiameter = 60, A5MaxDiameter = 137.46;

    public double HeightCm { get; } = heightCm;

    /// Radius (cm) at height z (cm from the bottom).
    public double RadiusAt(double z)
    {
        double rNeck = neckDiameterCm / 2, rMax = maxDiameterCm / 2;
        double k = HeightCm / A5Height, zMax = 100 * k, zNeck = 165 * k, rBottom = 10 * rMax / 68.73;
        if (z <= zMax) return rBottom + (rMax - rBottom) * Math.Sin(Math.PI / 2 * z / zMax);
        if (z < zNeck) return rNeck + (rMax - rNeck) * Math.Cos(Math.PI / 2 * (z - zMax) / (zNeck - zMax));
        return rNeck;
    }

    /// Volume of the shape as measured (before scaling to a known volume).
    public double RawTotalLitres() => Integrate(Heights(), RadiusAt)[^1];

    /// Calibration table [z_cm, radius_cm, cumulative_litres] every whole cm. targetLitres = known volume (radii scaled so the
    /// full volume matches) or null (the shape's own volume).
    public string ToCalibrationJson(double? targetLitres = A5TargetLitres, string model = "A5")
    {
        double raw = RawTotalLitres();
        double scale = targetLitres is > 0 ? Math.Sqrt(targetLitres.Value / raw) : 1;
        var zs = Heights();
        double R(double z) => RadiusAt(z) * scale;
        var cum = Integrate(zs, R);
        var table = new JsonArray();
        for (int i = 0; i < zs.Length; i++)
            table.Add(new JsonArray(Math.Round(zs[i], 3), Math.Round(R(zs[i]), 3), Math.Round(cum[i], 3)));
        return new JsonObject
        {
            ["kind"] = "qvevri", ["model"] = model, ["h_cm"] = HeightCm, ["raw_total_l"] = Math.Round(raw, 2), ["scale"] = Math.Round(scale, 6),
            ["table"] = table,
        }.ToJsonString();
    }

    /// Heights every whole cm, plus the exact top if the depth is fractional.
    double[] Heights()
    {
        var zs = Enumerable.Range(0, (int)Math.Floor(HeightCm) + 1).Select(i => (double)i).ToList();
        if (HeightCm - zs[^1] > 1e-9) zs.Add(HeightCm);
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
}
