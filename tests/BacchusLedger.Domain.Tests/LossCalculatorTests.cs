namespace BacchusLedger.Domain.Tests;

/// Section 6: mass balance of the dataset batches.
public class LossCalculatorTests
{
    [Fact]
    public void W01_loses_450_litres_in_total()
    {
        var r = SeedLosses.W01;
        Assert.Equal(450, r.TotalLossL);
        Assert.Equal(20, r.RemainderL);
        Assert.Equal(470, r.TotalDifferenceL);
        Assert.Equal(1345.50, r.LossValueUsd);
    }

    [Theory]
    [InlineData("racking_first", 280, 2.77)]
    [InlineData("stock_check", 0, 0.00)]
    [InlineData("racking", 100, 1.02)]
    [InlineData("filtration", 50, 0.51)]
    [InlineData("bottling", 20, 0.21)]
    public void W01_step_losses_are_within_normal(string stage, double lossL, double lossPct)
    {
        var s = SeedLosses.W01.Stages.Single(x => x.StageKey == stage);
        Assert.Equal(lossL, s.LossL);
        Assert.Equal(lossPct, s.LossPct);
        Assert.Equal("ok", s.Status);
    }

    [Fact]
    public void W01_bottle_check_and_give_out_check_pass()
    {
        var r = SeedLosses.W01;
        Assert.Equal(9630, r.BottledL);
        Assert.Equal("pass", r.Checks.Single(c => c.Key == "bottle").Status);
        Assert.Equal("pass", r.Checks.Single(c => c.Key == "give_out").Status);
    }

    [Fact]
    public void W03_flags_filtration_and_bottling_above_normal()
    {
        var r = SeedLosses.W03;
        var above = r.Stages.Where(s => s.Status == "above").Select(s => (s.StageKey, s.LossPct, Math.Round(s.ExcessL ?? 0, 1))).ToList();
        Assert.Equal([("filtration", 1.00, 4.8), ("bottling", 0.51, 2.4)], above);
        Assert.Equal(48, r.TotalLossL);
    }

    [Fact]
    public void A_vessel_cannot_give_out_more_than_it_holds()
    {
        var ops = SeedLosses.Operations(Dataset.SeedDataset.W01Ops.Take(3));
        ops[2] = new Losses.CellarOperation { Id = 3, Date = "2025-10-12", Type = "racking", FromCode = "T1-1", ToCode = "T1-2", Litres = 20000 };
        var r = new Losses.LossCalculator(SeedLosses.DefaultNorms, SeedLosses.ValuePerLitre).Replay(ops);
        Assert.Equal("fail", r.Checks.Single(c => c.Key == "give_out").Status);
        Assert.Equal("negative", r.Stages.Last().Status);
    }
}
