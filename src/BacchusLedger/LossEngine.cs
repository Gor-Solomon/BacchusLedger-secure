using BacchusLedger.Extensions;

namespace BacchusLedger;

/// Section 6: mass balance. Replays a batch's operations in date order, tracking litres per vessel.
public static class LossEngine
{
    public sealed class Op
    {
        public long Id;
        public string Date = "", Type = "";
        public long? FromId, ToId;
        public string? FromCode, ToCode, FromLabel, ToLabel, Method, Note, Source, Person;
        public double? Litres, Ml, Remainder, Dip;
        public int? Bottles;
    }

    public sealed class Stage
    {
        public long OpId { get; set; }
        public string Date { get; set; } = "";
        public string Type { get; set; } = "";
        public string StageKey { get; set; } = "";
        public string? From { get; set; }
        public string? To { get; set; }
        public string? FromLabel { get; set; }
        public string? ToLabel { get; set; }
        public double? BeforeL { get; set; }
        public double? AfterL { get; set; }
        public double? RemainderL { get; set; }
        public double? LossL { get; set; }
        public double? LossPct { get; set; }
        public double? NormPct { get; set; }
        public double? NormL { get; set; }
        public double? ExcessL { get; set; }
        public string Status { get; set; } = "n/a"; // ok | above | negative | n/a
        public int? Bottles { get; set; }
        public double? BottleMl { get; set; }
        public string? Method { get; set; }
        public double? DipCm { get; set; }
        public string? Note { get; set; }
        public string? Source { get; set; }
        public string? Person { get; set; }
        public StageComparison? Other { get; set; }
    }

    /// The same stage at the other winery (W01 vs W03).
    public sealed record StageComparison(string Winery, string Batch, double? LossL, double? LossPct, string Status, string? Source);

    public sealed class Check
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Status { get; set; } = "pass"; // pass | fail | info | na
        public string Detail { get; set; } = "";
        public string Source { get; set; } = "Calculated";
    }

    public sealed class Result
    {
        public long BatchId { get; set; }
        public string BatchCode { get; set; } = "";
        public string Winery { get; set; } = "";
        public List<Stage> Stages { get; set; } = [];
        public List<Check> Checks { get; set; } = [];
        public double? FirstLitres { get; set; }
        public double BottledL { get; set; }
        public int Bottles { get; set; }
        public double RemainderL { get; set; }
        public double TotalLossL { get; set; }
        public double? TotalDifferenceL { get; set; }
        public double LitresNow { get; set; }
        public double LossValueUsd { get; set; }
        public double ExcessL { get; set; }
        public Dictionary<string, double> VesselLitres { get; set; } = [];
    }

    public static readonly string[] NormKeys = ["racking_first", "racking", "filtration", "bottling", "stock_check", "transfer"];

    public static Dictionary<string, double> Norms(IReadOnlyDictionary<string, string> s) =>
        NormKeys.ToDictionary(k => k, k => s.Number("norm_" + k));

    static double R(double v, int d = 2) => Math.Round(v, d, MidpointRounding.AwayFromZero);

    /// Core replay over an op list (pure — used for preview, selftest and pages).
    public static Result Replay(List<Op> ops, Dictionary<string, double> norms, double valuePerLitre)
    {
        var res = new Result();
        var vessels = new Dictionary<string, double>();
        double Get(string? v) => v != null && vessels.TryGetValue(v, out var l) ? l : 0;
        bool firstRackingSeen = false;
        var giveOutFails = new List<string>();
        var bottleChecks = new List<(bool ok, string text)>();

        foreach (var o in ops.OrderBy(o => o.Date, StringComparer.Ordinal).ThenBy(o => o.Id))
        {
            var st = new Stage
            {
                OpId = o.Id, Date = o.Date, Type = o.Type, StageKey = o.Type, From = o.FromCode, To = o.ToCode, FromLabel = o.FromLabel, ToLabel = o.ToLabel,
                Bottles = o.Bottles, BottleMl = o.Ml, Method = o.Method, DipCm = o.Dip, Note = o.Note, Source = o.Source, Person = o.Person,
            };
            switch (o.Type)
            {
                case "separation":
                    st.AfterL = o.Litres;
                    if (o.ToCode != null) vessels[o.ToCode] = Get(o.ToCode) + (o.Litres ?? 0);
                    res.FirstLitres ??= o.Litres;
                    break;
                case "racking" or "filtration" or "transfer":
                {
                    if (o.Type == "racking") { st.StageKey = firstRackingSeen ? "racking" : "racking_first"; firstRackingSeen = true; }
                    double before = Get(o.FromCode), outL = o.Litres ?? 0, rem = o.Remainder ?? 0;
                    st.BeforeL = before; st.AfterL = outL; st.RemainderL = rem;
                    st.LossL = R(before - outL - rem);
                    if (outL + rem > before + 1e-9) giveOutFails.Add($"{o.Date} {o.FromCode}: gives {outL + rem:N0} L, holds {before:N0} L");
                    if (o.FromCode != null) vessels[o.FromCode] = rem;
                    if (o.ToCode != null) vessels[o.ToCode] = Get(o.ToCode) + outL;
                    break;
                }
                case "stock_check":
                {
                    var v = o.FromCode ?? o.ToCode;
                    double before = Get(v), measured = o.Litres ?? 0;
                    st.From = v; st.FromLabel = o.FromCode != null ? o.FromLabel : o.ToLabel; st.To = null; st.ToLabel = null; st.BeforeL = before; st.AfterL = measured;
                    st.LossL = R(before - measured);
                    if (v != null) vessels[v] = measured;
                    break;
                }
                case "bottling":
                {
                    double before = Get(o.FromCode), rem = o.Remainder ?? 0;
                    double outL = (o.Bottles ?? 0) * (o.Ml ?? 750) / 1000.0;
                    st.BeforeL = before; st.AfterL = R(outL); st.RemainderL = rem;
                    st.LossL = R(before - outL - rem);
                    if (outL + rem > before + 1e-9) giveOutFails.Add($"{o.Date} {o.FromCode}: gives {outL + rem:N0} L, holds {before:N0} L");
                    if (o.FromCode != null) vessels[o.FromCode] = rem;
                    res.BottledL += outL; res.Bottles += o.Bottles ?? 0; res.RemainderL += rem;
                    // Bottle check: bottles x bottle size = litres recorded as bottled
                    double recorded = o.Litres ?? outL;
                    bottleChecks.Add((Math.Abs(recorded - outL) < 0.5,
                        $"{o.Bottles:N0} × {(o.Ml ?? 750) / 1000.0:0.00#} = {outL:N0} L" + (o.Litres != null ? $" (recorded {o.Litres:N0} L)" : "")));
                    break;
                }
            }
            if (st.LossL != null && st.BeforeL is > 0)
            {
                double before = st.BeforeL.Value;
                st.LossPct = R(st.LossL.Value / before * 100);
                if (norms.TryGetValue(st.StageKey, out var norm))
                {
                    st.NormPct = norm;
                    st.NormL = R(norm / 100 * before);
                    st.ExcessL = R(Math.Max(0, st.LossL.Value - norm / 100 * before));
                    st.Status = st.LossL < 0 ? "negative" : st.ExcessL > 0.5 ? "above" : "ok";
                }
            }
            else if (st.LossL != null) st.Status = st.LossL < 0 ? "negative" : "n/a";
            res.Stages.Add(st);
        }

        res.TotalLossL = R(res.Stages.Where(s => s.LossL != null).Sum(s => s.LossL!.Value));
        res.ExcessL = R(res.Stages.Where(s => s.ExcessL != null).Sum(s => s.ExcessL!.Value));
        res.BottledL = R(res.BottledL);
        res.TotalDifferenceL = res.FirstLitres == null ? null : R(res.FirstLitres.Value - res.BottledL - (res.Bottles > 0 ? 0 : vessels.Values.Sum()));
        res.VesselLitres = vessels.Where(kv => kv.Value > 1e-9).ToDictionary(kv => kv.Key, kv => R(kv.Value));
        res.LitresNow = R(vessels.Values.Sum());
        res.LossValueUsd = R(res.TotalLossL * valuePerLitre);

        foreach (var (ok, text) in bottleChecks)
            res.Checks.Add(new Check { Key = "bottle", Label = "Bottle check: bottles × bottle size = litres bottled", Status = ok ? "pass" : "fail", Detail = text });
        res.Checks.Add(new Check
        {
            Key = "give_out", Label = "A vessel cannot give out more than it holds",
            Status = ops.Any(o => o.Type is "racking" or "filtration" or "transfer" or "bottling") ? (giveOutFails.Count == 0 ? "pass" : "fail") : "na",
            Detail = giveOutFails.Count == 0 ? "All transfers within vessel contents" : string.Join("; ", giveOutFails),
        });
        return res;
    }
}
