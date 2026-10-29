namespace BacchusLedger.Domain.Losses;

/// Section 6: mass balance. Losses per stage against the normal loss, the bottle check and the "a vessel cannot give out more than it holds" check.
public sealed class LossCalculator(StageNorms norms, double valuePerLitre)
{
    static double R(double v, int d = 2) => Math.Round(v, d, MidpointRounding.AwayFromZero);

    /// Replays the operations in date order, tracking litres per vessel.
    public LossResult Replay(List<CellarOperation> ops)
    {
        var res = new LossResult();
        var vessels = new Dictionary<string, double>();
        double Get(string? v) => v != null && vessels.TryGetValue(v, out var l) ? l : 0;
        bool firstRackingSeen = false;
        var giveOutFails = new List<string>();
        var bottleChecks = new List<(bool ok, string text)>();

        foreach (var o in ops.OrderBy(o => o.Date, StringComparer.Ordinal).ThenBy(o => o.Id))
        {
            var st = new LossStage
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
                if (norms.TryGet(st.StageKey, out var norm))
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
            res.Checks.Add(new LossCheck { Key = "bottle", Label = "Bottle check: bottles × bottle size = litres bottled", Status = ok ? "pass" : "fail", Detail = text });
        res.Checks.Add(new LossCheck
        {
            Key = "give_out", Label = "A vessel cannot give out more than it holds",
            Status = ops.Any(o => o.Type is "racking" or "filtration" or "transfer" or "bottling") ? (giveOutFails.Count == 0 ? "pass" : "fail") : "na",
            Detail = giveOutFails.Count == 0 ? "All transfers within vessel contents" : string.Join("; ", giveOutFails),
        });
        return res;
    }
}
