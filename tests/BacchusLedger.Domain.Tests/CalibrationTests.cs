using BacchusLedger.Domain.Vessels;

namespace BacchusLedger.Domain.Tests;

/// Section 5: dip reading ↔ litres.
public class CalibrationTests
{
    [Fact]
    public void Qvevri_raw_total_before_scaling_is_about_1490_litres() =>
        Assert.InRange(new QvevriProfile().RawTotalLitres(), 1475, 1505);

    [Fact]
    public void Qvevri_full_volume_is_1500_litres()
    {
        var q = VesselCalibration.Parse(new QvevriProfile().ToCalibrationJson());
        Assert.InRange(q.Capacity, 1499, 1501);
        Assert.Equal(1500, q.LitresAtDip(0), 1);
        Assert.Equal(0, q.LitresAtDip(q.HeightCm), 6);
    }

    [Fact]
    public void Qvevri_dip_and_litres_are_inverse()
    {
        var q = VesselCalibration.Parse(new QvevriProfile().ToCalibrationJson());
        var dip = q.DipForLitres(1230);
        Assert.Equal(1230, q.LitresAtDip(dip), 1);
    }

    [Fact]
    public void Tank_220_by_290_holds_about_11024_litres() =>
        Assert.Equal(11024, new CylinderTank(220, 290).Capacity, 0);

    [Fact]
    public void T3_tank_1188_litres_is_a_dip_of_35_cm()
    {
        var t3 = VesselCalibration.Parse(new CylinderTank(110, 160).ToCalibrationJson());
        Assert.Equal(35.0, Math.Round(t3.DipForLitres(1188), 1));
        Assert.Equal(1188, t3.LitresAtDip(35.0), 0);
    }

    [Fact]
    public void Dip_chart_has_a_row_every_5_cm()
    {
        var chart = VesselCalibration.Parse(new CylinderTank(110, 160).ToCalibrationJson()).DipChart(5);
        Assert.Equal(33, chart.Count);
        Assert.Equal(new DipChartPoint(0, 160, Math.Round(new CylinderTank(110, 160).Capacity, 1)), chart[0]);
    }
}
