using System.Text.Json.Nodes;
using BacchusLedger.Contracts.Cellar;

namespace BacchusLedger.Services;

public sealed class CellarService : ICellarService
{
    /// Vessels holding grapes between intake and separation are marked; totals per operation date find the fullest day.
    public Task<CellarResponse> GetAsync(string? date)
    {
        using var c = Db.Open();
        var s = Db.Settings();
        var day = date is { Length: 10 } ? date : DateTime.Now.ToString("yyyy-MM-dd");
        var batches = Db.Query(c, "SELECT b.*, w.code AS winery_code FROM batches b JOIN wineries w ON w.id=b.winery_id WHERE COALESCE(b.source_note,'') NOT LIKE '%copy%'");
        var litres = new Dictionary<string, List<CellarContentDto>>();
        var attention = new Dictionary<string, string>();
        var grapes = new Dictionary<string, CellarGrapesDto>();
        var opDates = new SortedSet<string>();
        var totals = new SortedDictionary<string, double>();
        foreach (var b in batches)
        {
            var ops = LossEngine.LoadOps(c, (long)b["id"]!);
            foreach (var o in ops) opDates.Add(o.Date);
            var r = LossEngine.Replay(ops.Where(o => string.CompareOrdinal(o.Date, day) <= 0).ToList(), LossEngine.Norms(s), Db.SettingD(s, "value_per_litre_usd"));
            foreach (var (v, l) in r.VesselLitres)
            {
                var key = $"{b["winery_code"]}/{v}";
                if (!litres.TryGetValue(key, out var list)) litres[key] = list = [];
                list.Add(new CellarContentDto(b["code"] as string, (long)b["id"]!, l, r.Stages.LastOrDefault(x => x.To == v || x.From == v)?.Source));
            }
            foreach (var st in r.Stages.Where(x => x.Status is "above" or "negative"))
                foreach (var v in new[] { st.From, st.To }.Where(x => x != null))
                    attention[$"{b["winery_code"]}/{v}"] = $"{b["code"]}: {st.StageKey.Replace('_', ' ')} {st.LossPct:0.00}% ({(st.Status == "above" ? "above normal" : "check the reading")})";
            var intake = ops.Where(o => o.Type == "intake" && o.ToCode != null && string.CompareOrdinal(o.Date, day) <= 0).LastOrDefault();
            if (intake != null && !ops.Any(o => o.Type == "separation" && o.FromCode == intake.ToCode && string.CompareOrdinal(o.Date, day) <= 0))
                grapes[$"{b["winery_code"]}/{intake.ToCode}"] = new CellarGrapesDto(b["code"] as string, (long)b["id"]!, Db.D(b["grapes_kg"]), intake.Date, intake.Source);
        }
        foreach (var d in opDates)
        {
            double tot = 0;
            foreach (var b in batches)
                tot += LossEngine.Replay(LossEngine.LoadOps(c, (long)b["id"]!).Where(o => string.CompareOrdinal(o.Date, d) <= 0).ToList(), LossEngine.Norms(s), 0).LitresNow;
            totals[d] = Math.Round(tot, 1);
        }
        var vessels = Db.Query(c, "SELECT v.*, w.code AS winery_code FROM vessels v JOIN wineries w ON w.id=v.winery_id ORDER BY w.code, v.type DESC, v.id").Select(v =>
        {
            var key = $"{v["winery_code"]}/{v["code"]}";
            var m = Calibration.TryParse(v["calibration_json"] as string);
            // outline for drawing: radius / max radius at z / H (A5 profile for qvevri; generalised A5 shape when a qvevri has no measurements — drawing only)
            var model = m?.Kind == "qvevri" ? m : (string)v["type"]! == "qvevri" ? Calibration.Parse(Calibration.QvevriJson()) : null;
            List<double[]>? outline = null;
            if (model != null)
            {
                var t = JsonNode.Parse(m?.Kind == "qvevri" ? (string)v["calibration_json"]! : Calibration.QvevriJson())!["table"]!.AsArray();
                double rmax = t.Max(r => (double)r![1]!), h = model.H;
                outline = t.Where((r, i) => (double)r![0]! % 10 == 0 || i == t.Count - 1).Select(r => new[] { Math.Round((double)r![0]! / h, 4), Math.Round((double)r![1]! / rmax, 4) }).ToList();
            }
            var list = litres.GetValueOrDefault(key) ?? [];
            return new CellarVesselDto((long)v["id"]!, v["winery_code"] as string, v["code"] as string, v["cellar_label"] as string, v["name"] as string, v["type"] as string,
                Db.D(v["capacity_l"]), m == null ? null : m.Kind == "cylinder" ? v["source"] as string : "Dataset A5", m != null,
                m?.Kind == "cylinder" ? m.D : null, m?.H, outline, Math.Round(list.Sum(x => x.Litres), 1), list, grapes.GetValueOrDefault(key),
                attention.ContainsKey(key) ? "amber" : "ok", attention.GetValueOrDefault(key));
        }).ToList();
        var peak = totals.Count == 0 ? day : totals.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
        return Task.FromResult(new CellarResponse(day, vessels, opDates, totals, peak, totals.GetValueOrDefault(peak)));
    }
}
