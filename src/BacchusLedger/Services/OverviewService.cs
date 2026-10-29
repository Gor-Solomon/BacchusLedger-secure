using BacchusLedger.Contracts.Overview;

namespace BacchusLedger.Services;

public sealed class OverviewService : IOverviewService
{
    public Task<OverviewResponse> GetAsync()
    {
        using var c = Db.Open();
        var s = Db.Settings();
        var batches = Db.Query(c, "SELECT b.*, w.code AS winery_code FROM batches b JOIN wineries w ON w.id=b.winery_id ORDER BY b.code");
        double inCellar = 0, loss = 0, value = 0; int real = 0;
        var flags = new List<object>();
        foreach (var b in batches)
        {
            var r = LossEngine.Analyze(c, (long)b["id"]!, s, compare: false);
            bool copy = Db.IsCopy(b);
            var code = (string)b["code"]!; var id = (long)b["id"]!;
            if (!copy) { real++; inCellar += r.LitresNow; loss += r.TotalLossL; value += r.LossValueUsd; }
            foreach (var st in r.Stages.Where(x => x.Status is "above" or "negative"))
                flags.Add(new StageFlag(code, id, copy, st.Status, st.Date, st.StageKey, st.LossL, st.LossPct, st.NormPct, st.ExcessL,
                    st.Status == "above" ? "Above normal" : "More than recorded — check the reading", "Calculated"));
            foreach (var ch in r.Checks.Where(x => x.Status is "fail" or "info" && x.Key != "origin"))
                flags.Add(new CheckFlag(code, id, copy, ch.Status, ch.Label, ch.Detail, ch.Source));
        }
        var kpi = new OverviewKpi(Math.Round(inCellar, 1), real, Math.Round(loss, 1), Math.Round(value, 2));
        var onePercent = Money.OnePercentTable(c, Db.SettingD(s, "value_per_litre_usd"), Db.SettingD(s, "litres_per_tonne"));
        return Task.FromResult(new OverviewResponse(kpi, flags, onePercent, s));
    }

    public Task<IReadOnlyList<WineryDto>> GetWineriesAsync() =>
        Task.FromResult<IReadOnlyList<WineryDto>>(Db.Query("SELECT * FROM wineries ORDER BY code")
            .Select(r => new WineryDto((long)r["id"]!, r["code"] as string, r["name"] as string, r["size"] as string, Db.D(r["yearly_t"]), Db.L(r["uses_qvevri"]), r["readiness_level"] as string))
            .ToList());
}
