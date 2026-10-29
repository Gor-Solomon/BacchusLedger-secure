using BacchusLedger.Contracts.Report;
using BacchusLedger.Repositories;

namespace BacchusLedger.Services;

public sealed class ReportService(
    IBatchRepository batches, IVesselRepository vessels, IOriginRepository origins, ILabRepository lab, IReadingRepository readings,
    ILiveEventRepository liveEvents, ILossAnalysisService losses) : IReportService
{
    public async Task<ServiceResult<object>> GetAsync(string? batch, string? vessel) =>
        await BuildAsync(string.IsNullOrWhiteSpace(batch) ? null : batch, string.IsNullOrWhiteSpace(vessel) ? null : vessel) is { } r
            ? ServiceResult<object>.Ok(r) : ServiceResult<object>.NotFound("batch not found");

    /// The double-entry ledger of one batch (null when the batch does not exist).
    public async Task<LedgerReport.Ledger?> LedgerAsync(long batchId)
    {
        if (await batches.GetAsync(batchId) is not { } b) return null;
        var eng = await losses.AnalyzeAsync(batchId, compare: false);
        return LedgerReport.Build(b.GrapesKg, await origins.ListLedgerOriginsAsync(batchId), eng);
    }

    /// ReportResponse, VesselReportResponse (no batch given) or null (batch not found).
    public async Task<object?> BuildAsync(string? batchCode, string? vessel)
    {
        SimulatedSectionDto? simulated = null;
        string? vesselName = null;
        if (vessel != null && vessel.Split('/') is [var w, var code] && await vessels.GetAsync(w, code) is { } vr)
        {
            vesselName = LedgerReport.VesselName(vr.Code, vr.CellarLabel);
            simulated = new SimulatedSectionDto(vesselName, "SIMULATED", "Simulated live data (live_events). Not part of the ledger, the balance or any journal.",
                await liveEvents.ListVesselNotificationsAsync(vr.Id), await liveEvents.CountVesselReadingsAsync(vr.Id));
        }
        var br = batchCode == null ? null : await batches.GetByCodeAsync(batchCode);
        if (br == null) return batchCode != null ? null : new VesselReportResponse(null, vesselName, simulated, DateTime.UtcNow.ToString("o"));

        var l = (await LedgerAsync(br.Id))!;
        var eng = l.Engine;
        var timeline = new List<TimelineRowDto>();
        var harvest = l.Entries.Where(e => e.Kind == "harvest").ToList();
        if (harvest.Count > 0)
            timeline.Add(new(harvest[0].Date, "Harvest", $"{harvest.Count} {(harvest.Count == 1 ? "delivery" : "deliveries")} → reception", $"{harvest.Sum(e => e.Amount):N0} kg"));
        foreach (var s in eng.Stages)
            timeline.Add(new(s.Date, s.StageKey.Replace('_', ' '),
                s.Type == "stock_check" ? s.From : s.From != null && s.To != null ? $"{s.From} → {s.To}" : s.To ?? s.From ?? "—",
                s.Type switch
                {
                    "intake" => l.Entries.FirstOrDefault(e => e.OpId == s.OpId)?.Amount is { } kg ? $"{kg:N0} kg" : s.Note ?? "—",
                    "bottling" or "warehouse" => $"{s.Bottles:N0} bottles" + (s.AfterL != null ? $" · {s.AfterL:N0} L" : ""),
                    _ => s.AfterL != null ? $"{s.AfterL:N0} L" + (s.LossL is > 0 ? $" (−{s.LossL:N0} L)" : "") : "—",
                }));
        var batch = new ReportBatchDto(br.Code, br.Name, br.WineryCode, br.WineryName, br.WineType, br.Technology, br.Variety, br.Vintage, br.Abv, br.Status, br.GrapesKg, br.SourceNote);
        var entries = l.Entries.Select(e => new LedgerEntryDto(e.No, e.OpId, e.Ref, e.Date, e.Kind, e.Text, e.Source, e.Ratio,
            new LedgerSideDto(e.Debit, e.Amount, e.Unit), new LedgerSideDto(e.Credit, e.CreditAmount ?? e.Amount, e.CreditUnit ?? e.Unit))).ToList();
        var accounts = l.Accounts.Select(a => new LedgerAccountDto(a.Name, a.Unit, a.Debit, a.Credit, a.Balance)).ToList();
        var bal = l.Balance;
        var balance = new LedgerBalanceDto(bal.GrapesKg, bal.HarvestedKg, bal.PressedKg, bal.GrapesLeftKg, bal.KgGap, bal.InL, bal.BottledL, bal.InVesselsL, bal.LossesL, bal.GapL,
            bal.HasLitres, eng.TotalLossL, bal.EngineMatches, bal.Balanced, eng.Checks.Select(x => new BalanceCheckDto(x.Label, x.Status, x.Detail)).ToList());
        return new ReportResponse(batch, vesselName, timeline, entries, accounts, balance, await lab.ListForReportAsync(br.Id), await readings.ListForReportAsync(br.Id),
            simulated, DateTime.UtcNow.ToString("o"));
    }
}
