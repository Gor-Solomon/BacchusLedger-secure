using System.Text.Json.Nodes;

namespace BacchusLedger.Domain.Vessels;

/// A cylindrical tank (inner diameter and height in cm).
public sealed record CylinderTank(double DiameterCm, double HeightCm)
{
    public double Capacity => LitresAtHeight(HeightCm);

    /// Litres for a fill height in cm (not clamped).
    public double LitresAtHeight(double fillCm) => Math.PI * Math.Pow(DiameterCm / 2, 2) * fillCm / 1000.0;

    /// Fill height in cm for a volume in litres (not clamped).
    public double HeightForLitres(double litres) => litres * 1000.0 / (Math.PI * Math.Pow(DiameterCm / 2, 2));

    /// The calibration_json stored on the vessel.
    public string ToCalibrationJson() => new JsonObject { ["kind"] = "cylinder", ["d_cm"] = DiameterCm, ["h_cm"] = HeightCm }.ToJsonString();
}
