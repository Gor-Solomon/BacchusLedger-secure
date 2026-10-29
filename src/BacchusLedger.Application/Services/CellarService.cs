using System.Text.Json.Nodes;
using BacchusLedger.Application.Contracts.Cellar;
using BacchusLedger.Application.Extensions;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Domain.Vessels;
using BacchusLedger.Domain.Losses;

namespace BacchusLedger.Application.Services;

public sealed class CellarService(IBatchRepository batches, IVesselRepository vessels, IOperationRepository operations, ISettingsRepository settings) : ICellarService
{
    /// Vessels holding grapes between intake and separation are marked; totals per operation date find the fullest day.
    public async Task<CellarResponse> GetAsync(string? date)
    {
        var s = await settings.GetAsync();
        var day = date is { Length: 10 } ? date : DateTime.Now.ToString("yyyy-MM-dd");
        var real = await batches.ListRealAsync();
        var ops = new Dictionary<long, List<CellarOperation>>();
        var loaded = await operations.ListForBatchesAsync(real.Select(b => b.Id).ToList());
        foreach (var b in real) ops[b.Id] = loaded.GetValueOrDefault(b.Id) ?? [];
        var litres = new Dictionary<string, List<CellarContentDto>>();
        var attention = new Dictionary<string, string>();
        var grapes = new Dictionary<string, CellarGrapesDto>();
        var opDates = new SortedSet<string>();
        var totals = new SortedDictionary<string, double>();
        foreach (var b in real)
        {
            var bo = ops[b.Id];
            foreach (var o in bo) opDates.Add(o.Date);
            var r = new LossCalculator(s.Norms(), s.Number("value_per_litre_usd")).Replay(bo.Where(o => string.CompareOrdinal(o.Date, day) <= 0).ToList());
            foreach (var (v, l) in r.VesselLitres)
            {
                var key = $"{b.WineryCode}/{v}";
                if (!litres.TryGetValue(key, out var list)) litres[key] = list = [];
                list.Add(new CellarContentDto(b.Code, b.Id, l, r.Stages.LastOrDefault(x => x.To == v || x.From == v)?.Source));
            }
            foreach (var st in r.Stages.Where(x => x.Status is "above" or "negative"))
                foreach (var v in new[] { st.From, st.To }.Where(x => x != null))
                    attention[$"{b.WineryCode}/{v}"] = $"{b.Code}: {st.StageKey.Replace('_', ' ')} {st.LossPct:0.00}% ({(st.Status == "above" ? "above normal" : "check the reading")})";
            var intake = bo.Where(o => o.Type == "intake" && o.ToCode != null && string.CompareOrdinal(o.Date, day) <= 0).LastOrDefault();
            if (intake != null && !bo.Any(o => o.Type == "separation" && o.FromCode == intake.ToCode && string.CompareOrdinal(o.Date, day) <= 0))
                grapes[$"{b.WineryCode}/{intake.ToCode}"] = new CellarGrapesDto(b.Code, b.Id, b.GrapesKg, intake.Date, intake.Source);
        }
        foreach (var d in opDates)
            totals[d] = Math.Round(real.Sum(b => new LossCalculator(s.Norms(), 0).Replay(ops[b.Id].Where(o => string.CompareOrdinal(o.Date, d) <= 0).ToList()).LitresNow), 1);
        var list2 = (await vessels.ListForCellarAsync()).Select(v =>
        {
            var key = $"{v.WineryCode}/{v.Code}";
            var m = VesselCalibration.TryParse(v.CalibrationJson);
            // outline for drawing: radius / max radius at z / H (A5 profile for qvevri; generalised A5 shape when a qvevri has no measurements, drawing only)
            var model = m?.Kind == "qvevri" ? m : v.Type == "qvevri" ? VesselCalibration.Parse(new QvevriProfile().ToCalibrationJson()) : null;
            List<double[]>? outline = null;
            if (model != null)
            {
                var t = JsonNode.Parse(m?.Kind == "qvevri" ? v.CalibrationJson! : new QvevriProfile().ToCalibrationJson())!["table"]!.AsArray();
                double rmax = t.Max(r => (double)r![1]!), h = model.HeightCm;
                outline = t.Where((r, i) => (double)r![0]! % 10 == 0 || i == t.Count - 1).Select(r => new[] { Math.Round((double)r![0]! / h, 4), Math.Round((double)r![1]! / rmax, 4) }).ToList();
            }
            var contents = litres.GetValueOrDefault(key) ?? [];
            return new CellarVesselDto(v.Id, v.WineryCode, v.Code, v.CellarLabel, v.Name, v.Type, v.CapacityL, m == null ? null : m.Kind == "cylinder" ? v.Source : "Dataset A5",
                m != null, m?.Kind == "cylinder" ? m.DiameterCm : null, m?.HeightCm, outline, Math.Round(contents.Sum(x => x.Litres), 1), contents, grapes.GetValueOrDefault(key),
                attention.ContainsKey(key) ? "amber" : "ok", attention.GetValueOrDefault(key));
        }).ToList();
        var peak = totals.Count == 0 ? day : totals.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
        return new CellarResponse(day, list2, opDates, totals, peak, totals.GetValueOrDefault(peak));
    }
}
