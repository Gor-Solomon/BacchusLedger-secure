using BacchusLedger.Domain.Dataset;
using BacchusLedger.Domain.Ledger;

namespace BacchusLedger.Domain.Tests;

public class DoubleEntryBookingTests
{
    static Ledger.Ledger W01Ledger()
    {
        var deliveries = SeedDataset.W01Farmers
            .Select((f, i) => new HarvestDelivery(i + 1, "2025-09-18", f.Kg, "DEMO ASSUMPTION", f.Plot, SeedDataset.Municipality, f.Farmer)).ToList();
        return new DoubleEntryBooking().Book(16000, deliveries, SeedLosses.W01);
    }

    [Fact]
    public void W01_balances_in_equals_out_plus_losses()
    {
        var b = W01Ledger().Balance;
        Assert.True(b.Balanced);
        Assert.Equal(10100, b.InL);
        Assert.Equal(9650, b.BottledL + b.InVesselsL);
        Assert.Equal(450, b.LossesL);
        Assert.Equal(0, b.GapL);
        Assert.Equal(0, b.KgGap);
        Assert.True(b.EngineMatches);
    }

    [Fact]
    public void Debits_equal_credits_except_the_pressing_that_turns_kg_into_litres()
    {
        var l = W01Ledger();
        Assert.All(l.Entries.Where(e => e.Kind != "transformation"), e => Assert.Null(e.CreditAmount));
        double Net(string unit) => l.Accounts.Where(a => a.Unit == unit).Sum(a => a.Debit - a.Credit);
        var pressing = l.Entries.Where(e => e.Kind == "transformation").ToList();
        Assert.Equal(pressing.Sum(e => e.Amount), Net("L"), 6);
        Assert.Equal(-pressing.Sum(e => e.CreditAmount ?? 0), Net("kg"), 6);
    }

    [Fact]
    public void Pressing_turns_16000_kg_into_10100_litres()
    {
        var t = W01Ledger().Entries.Single(e => e.Kind == "transformation");
        Assert.Equal(10100, t.Amount);
        Assert.Equal("L", t.Unit);
        Assert.Equal(16000, t.CreditAmount);
        Assert.Equal("kg", t.CreditUnit);
    }
}
