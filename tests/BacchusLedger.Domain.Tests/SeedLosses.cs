using System.Globalization;
using BacchusLedger.Domain.Dataset;
using BacchusLedger.Domain.Losses;

namespace BacchusLedger.Domain.Tests;

/// The dataset batches replayed with the default (DEMO ASSUMPTION) norms and $2.99 per litre.
public static class SeedLosses
{
    public const double ValuePerLitre = 2.99;

    public static StageNorms DefaultNorms { get; } =
        new(StageNorms.Keys.ToDictionary(k => k, k => double.Parse(SettingDefaults.Values["norm_" + k], CultureInfo.InvariantCulture)));

    public static LossResult W01 => Replay(SeedDataset.W01Ops);
    public static LossResult W03 => Replay(SeedDataset.W03Ops);

    public static List<CellarOperation> Operations(IEnumerable<SeedOperation> ops) => ops.Select((o, i) => new CellarOperation
    {
        Id = i + 1, Date = o.Date, Type = o.Type, FromCode = o.From, ToCode = o.To, Litres = o.Litres, Bottles = o.Bottles, Ml = o.Ml, Remainder = o.Remainder,
        Source = "Dataset",
    }).ToList();

    static LossResult Replay(IEnumerable<SeedOperation> ops) => new LossCalculator(DefaultNorms, ValuePerLitre).Replay(Operations(ops));
}
