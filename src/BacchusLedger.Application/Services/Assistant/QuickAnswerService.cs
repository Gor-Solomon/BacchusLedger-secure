using BacchusLedger.Application.Contracts.Assistant;
using BacchusLedger.Domain.Dataset;
using BacchusLedger.Application.Extensions;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;
using Microsoft.Extensions.Logging;
using static BacchusLedger.Application.Services.Assistant.AssistantTexts;
using BacchusLedger.Domain.Common;

namespace BacchusLedger.Application.Services.Assistant;

public sealed class QuickAnswerService(
    IBatchRepository batches, IOriginRepository origins, IReadingRepository readings, ILiveEventRepository liveEvents, ILossAnalysisService losses,
    ILogger<QuickAnswerService> logger) : IQuickAnswerService
{
    public async Task<AssistantAnswer> AnswerAsync(int n, string lang, IReadOnlyDictionary<string, string> s)
    {
        try
        {
            return n switch { 1 => await Q1(s, lang), 2 => await Q2(s, lang), 3 => await Q3(lang), 4 => await Q4(lang), _ => await Q5(lang) };
        }
        catch (Exception e)
        {
            logger.LogError("assistant: quick question {N} failed ({Error})", n, e.GetType().Name);
            return new() { Text = NoData(lang) };
        }
    }

    public async Task<W01Loss?> W01LossesAsync(IReadOnlyDictionary<string, string> s)
    {
        if (await batches.FindIdAsync(SeedDataset.W01Batch) is not { } id) return null;
        var r = await losses.AnalyzeAsync(id, s, compare: false);
        var steps = r.Stages.Where(x => x.LossL != null).Select(x => new LossStep(x.StageKey, x.Date, x.LossL!.Value, x.LossPct, x.Status)).ToList();
        return new(id, r.BatchCode, steps, r.TotalLossL, r.RemainderL, r.LossValueUsd, s.Number("value_per_litre_usd"));
    }

    public async Task<List<AboveNormalFlag>> AboveNormalAsync(IReadOnlyDictionary<string, string> s)
    {
        var list = new List<AboveNormalFlag>();
        foreach (var b in await batches.ListAsync())
        {
            if (b.IsCopy) continue;
            var r = await losses.AnalyzeAsync(b.Id, s, compare: false);
            list.AddRange(r.Stages.Where(x => x.Status == "above")
                .Select(x => new AboveNormalFlag(b.Id, b.Code, x.StageKey, x.Date, x.LossL, x.LossPct, x.NormPct, x.ExcessL, x.Source)));
        }
        return list;
    }

    static string StageName(string key, int racking, string lang) => key switch
    {
        "racking_first" => L(lang, "First racking", "ლექიდან მოხსნა (I)"),
        "racking" => L(lang, racking == 1 ? "2nd racking" : $"Racking #{racking + 1}", $"ლექიდან მოხსნა ({(racking == 1 ? "II" : (racking + 1).ToString())})"),
        "stock_check" => L(lang, "Stock check", "ნაშთის შემოწმება"),
        "filtration" => L(lang, "Filtration", "ფილტრაცია"),
        "bottling" => L(lang, "Bottling", "ჩამოსხმა"),
        "transfer" => L(lang, "Transfer", "გადატანა"),
        _ => key,
    };

    async Task<AssistantAnswer> Q1(IReadOnlyDictionary<string, string> s, string lang)
    {
        if (await W01LossesAsync(s) is not { } w) return new() { Text = NoData(lang) };
        int rk = 0;
        var lines = w.Steps.Select(x => $"• {StageName(x.Stage, x.Stage == "racking" ? ++rk : 0, lang)}: {F(x.LossL)} {L(lang, "L", "ლ")}").ToList();
        var text = L(lang, $"{w.Code} lost {F(w.TotalLossL)} L in total, plus {F(w.RemainderL)} L left in the tank:",
                           $"{w.Code}: სულ დაიკარგა {F(w.TotalLossL)} ლ, ავზში დარჩა {F(w.RemainderL)} ლ:")
                   + "\n" + string.Join("\n", lines) + "\n"
                   + L(lang, $"Value of the loss: ${F(w.ValueUsd, 2)} at ${F(w.ValuePerLitre, 2)}/L.", $"დანაკარგის ღირებულება: ${F(w.ValueUsd, 2)} (${F(w.ValuePerLitre, 2)}/ლ).");
        return new() { Text = text, Sources = ["Dataset A6.2", "Calculated"], Link = $"#/batch/{w.BatchId}", LinkLabel = w.Code, Used = [$"batch {w.Code}"] };
    }

    async Task<AssistantAnswer> Q2(IReadOnlyDictionary<string, string> s, string lang)
    {
        var flags = await AboveNormalAsync(s);
        if (flags.Count == 0) return new() { Text = L(lang, "No step is above normal.", "ნორმაზე მეტი ეტაპი არ არის."), Sources = ["Calculated", "DEMO ASSUMPTION"], Link = "#/overview", Used = ["all batches"] };
        var lines = flags.Select(f => $"• {f.Batch} · {StageName(f.Stage, 1, lang)}: {F(f.LossPct, 2)}% " +
            L(lang, $"(normal {F(f.NormPct, 1)}%), {F(f.ExcessL, 1)} L extra", $"(ნორმა {F(f.NormPct, 1)}%), {F(f.ExcessL, 1)} ლ ზედმეტი"));
        var text = L(lang, $"{flags.Count} step{(flags.Count == 1 ? " is" : "s are")} above normal:", $"ნორმაზე მეტია {flags.Count} ეტაპი:") + "\n" + string.Join("\n", lines) + "\n" +
                   L(lang, "Normal loss per step is a DEMO ASSUMPTION (Settings).", "ნორმალური დანაკარგი — DEMO ASSUMPTION (პარამეტრები).");
        var one = flags.Select(f => f.BatchId).Distinct().Count() == 1;
        return new()
        {
            Text = text, Sources = ["Calculated", "DEMO ASSUMPTION", .. flags.Select(f => f.Source).OfType<string>().Distinct()],
            Link = one ? $"#/batch/{flags[0].BatchId}" : "#/overview", LinkLabel = one ? flags[0].Batch : null, Used = ["all batches"],
        };
    }

    async Task<AssistantAnswer> Q3(string lang)
    {
        if (await batches.FindIdAsync(SeedDataset.W01Batch) is not { } id) return new() { Text = NoData(lang) };
        var b = (await batches.GetAsync(id))!;
        var rows = await origins.ListFarmerOriginsAsync(id);
        if (rows.Count == 0) return new() { Text = NoData(lang) };
        double kg = b.GrapesKg ?? rows.Sum(r => r.Kg ?? 0);
        var lines = rows.Select(r => $"• {r.Farmer} · {L(lang, "plot", "ნაკვეთი")} {r.Plot} ({r.Municipality}): {F(r.Kg)} kg ({F(r.Kg / kg * 100, 1)}%)");
        var text = L(lang, $"{b.Code}: {F(kg)} kg of grapes from {rows.Count} farmers. The split is a DEMO ASSUMPTION — the dataset has no farmers:",
                           $"{b.Code}: {F(kg)} კგ ყურძენი {rows.Count} ფერმერისგან. გადანაწილება — DEMO ASSUMPTION (მონაცემებში ფერმერები არ არის):")
                   + "\n" + string.Join("\n", lines);
        return new() { Text = text, Sources = ["Dataset A2", "DEMO ASSUMPTION"], Link = $"#/batch/{id}", LinkLabel = b.Code, Used = [$"batch {b.Code}"] };
    }

    async Task<AssistantAnswer> Q4(string lang)
    {
        if (await batches.FindIdAsync(SeedDataset.W03Batch) is not { } id) return new() { Text = NoData(lang) };
        var rows = await readings.ListBasicAsync(id);
        var latest = new[] { "temperature", "ph", "ta" }.Select(t => rows.LastOrDefault(r => r.Type == t)).OfType<LatestReading>().ToList();
        if (latest.Count == 0) return new() { Text = NoData(lang) };
        string Name(string t) => t switch { "temperature" => L(lang, "Temperature", "ტემპერატურა"), "ph" => "pH", _ => L(lang, "TA", "ტიტრული მჟავიანობა") };
        var lines = latest.Select(r => $"• {Name(r.Type)}: {F(r.Value)}{(r.Unit is { } u && u != "pH" ? " " + u : "")} " +
                                       $"({r.TakenAt.ToDisplayDate()}, {r.Vessel.WithCellarLabel(r.CellarLabel)})");
        var text = L(lang, "W03-B3-2025 — latest readings:", "W03-B3-2025 — ბოლო გაზომვები:") + "\n" + string.Join("\n", lines);
        return new()
        {
            Text = text, Sources = latest.Select(r => r.Source).OfType<string>().Distinct().ToList(),
            Link = $"#/batch/{id}?readings=1", LinkLabel = L(lang, "charts", "გრაფიკები"), Used = ["readings W03-B3-2025"],
        };
    }

    async Task<AssistantAnswer> Q5(string lang)
    {
        var rows = await liveEvents.ListOpenAlertsAsync(5);
        if (rows.Count == 0)
            return new() { Text = L(lang, "No alerts.", "შეტყობინებები არ არის."), Sources = ["SIMULATED"], Link = "#/live", LinkLabel = L(lang, "Live board", "Live დაფა"), Used = ["live alerts"] };
        var lines = rows.Select(r => $"• {r.Level?.ToUpperInvariant()} · {r.VesselName ?? r.VesselCode}: {r.Message} ({(r.SimTime ?? "").ToDisplayDateTime()})");
        var text = L(lang, $"{rows.Count} open live alert{(rows.Count == 1 ? "" : "s")} (SIMULATED data):", $"{rows.Count} ღია შეტყობინება (SIMULATED მონაცემები):") + "\n" + string.Join("\n", lines);
        return new()
        {
            Text = text, Sources = rows.Select(r => r.Source).OfType<string>().Distinct().ToList(),
            Link = "#/live", LinkLabel = L(lang, "Live board", "Live დაფა"), Used = ["live alerts"],
        };
    }
}
