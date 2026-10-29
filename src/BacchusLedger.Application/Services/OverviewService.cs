using BacchusLedger.Application.Contracts.Overview;
using BacchusLedger.Application.Extensions;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Domain.Money;
using BacchusLedger.Application.Services.Mapping;

namespace BacchusLedger.Application.Services;

public sealed class OverviewService(IBatchRepository batches, IWineryRepository wineries, ISettingsRepository settings, ILossAnalysisService losses) : IOverviewService
{
    public async Task<OverviewResponse> GetAsync()
    {
        var s = await settings.GetAsync();
        double inCellar = 0, loss = 0, value = 0; int real = 0;
        var flags = new List<object>();
        foreach (var b in await batches.ListAsync())
        {
            var r = await losses.AnalyzeAsync(b.Id, s, compare: false);
            if (!b.IsCopy) { real++; inCellar += r.LitresNow; loss += r.TotalLossL; value += r.LossValueUsd; }
            foreach (var st in r.Stages.Where(x => x.Status is "above" or "negative"))
                flags.Add(new StageFlag(b.Code, b.Id, b.IsCopy, st.Status, st.Date, st.StageKey, st.LossL, st.LossPct, st.NormPct, st.ExcessL,
                    st.Status == "above" ? "Above normal" : "More than recorded — check the reading", "Calculated"));
            foreach (var ch in r.Checks.Where(x => x.Status is "fail" or "info" && x.Key != "origin"))
                flags.Add(new CheckFlag(b.Code, b.Id, b.IsCopy, ch.Status, ch.Label, ch.Detail, ch.Source));
        }
        var kpi = new OverviewKpi(Math.Round(inCellar, 1), real, Math.Round(loss, 1), Math.Round(value, 2));
        var onePercent = new OnePercentLoss(s.Number("value_per_litre_usd"), s.Number("litres_per_tonne")).ToRows(await wineries.ListBySizeAsync());
        return new OverviewResponse(kpi, flags, onePercent, s);
    }

    public async Task<IReadOnlyList<WineryDto>> GetWineriesAsync() => await wineries.ListAsync();
}
