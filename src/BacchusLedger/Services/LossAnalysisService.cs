using BacchusLedger.Dataset;
using BacchusLedger.Extensions;
using BacchusLedger.Repositories;

namespace BacchusLedger.Services;

public sealed class LossAnalysisService(
    IBatchRepository batches, IOperationRepository operations, IOriginRepository origins, ISettingsRepository settingsRepository) : ILossAnalysisService
{
    public async Task<LossEngine.Result> AnalyzeAsync(long batchId, IReadOnlyDictionary<string, string>? settings = null, LossEngine.Op? extra = null,
        bool compare = true, Dictionary<string, double>? norms = null)
    {
        settings ??= await settingsRepository.GetAsync();
        norms ??= LossEngine.Norms(settings);
        var b = await batches.GetAsync(batchId) ?? throw new InvalidOperationException($"batch {batchId} not found");
        var ops = await operations.ListForBatchAsync(batchId);
        if (extra != null) ops.Add(extra);
        var res = LossEngine.Replay(ops, norms, settings.Number("value_per_litre_usd"));
        res.BatchId = batchId; res.BatchCode = b.Code; res.Winery = b.WineryCode;

        // Same stage at the other winery (W01 vs W03): pure dataset fact
        var refCode = res.Winery switch { "W01" => SeedDataset.W03Batch, "W03" => SeedDataset.W01Batch, _ => null };
        if (compare && refCode != null && await batches.FindIdAsync(refCode) is { } refId)
        {
            var other = await AnalyzeAsync(refId, settings, null, false, norms);
            foreach (var s in res.Stages)
            {
                var m = other.Stages.FirstOrDefault(x => x.StageKey == s.StageKey && x.LossL != null);
                if (m != null && s.LossL != null)
                    s.Other = new LossEngine.StageComparison(other.Winery, other.BatchCode, m.LossL, m.LossPct, m.Status, m.Source);
            }
        }

        var rows = await origins.ListOriginChecksAsync(batchId);
        if (rows.Count == 0)
        {
            res.Checks.Add(new LossEngine.Check { Key = "pdo", Label = "Plot municipality is inside the Kakheti PDO zone", Status = "na", Detail = "No plots recorded for this batch", Source = "Kakheti PDO spec" });
            res.Checks.Add(new LossEngine.Check
            {
                Key = "origin", Label = "Origin completeness: batch kg = sum of its deliveries", Status = "na",
                Detail = b.GrapesKg == null ? "No grape intake recorded for this batch" : $"{b.GrapesKg:N0} kg in Dataset A2 — no deliveries recorded (the dataset has no farmers)",
                Source = b.GrapesKg == null ? "Calculated" : "Dataset A2",
            });
        }
        else
        {
            var outside = rows.Where(o => !SeedDataset.PdoMunicipalities.Contains(o.Municipality ?? "")).Select(o => $"{o.Plot} ({o.Municipality})").ToList();
            var munis = string.Join(", ", rows.Select(o => o.Municipality).Distinct());
            res.Checks.Add(new LossEngine.Check
            {
                Key = "pdo", Label = "Plot municipality is inside the Kakheti PDO zone", Status = outside.Count == 0 ? "pass" : "fail",
                Detail = outside.Count == 0 ? $"{munis} is in the §3 list" : "Outside zone: " + string.Join(", ", outside), Source = "Kakheti PDO spec",
            });
            double kg = b.GrapesKg ?? 0, sum = rows.Sum(o => o.Kg ?? 0);
            res.Checks.Add(new LossEngine.Check
            {
                Key = "origin", Label = "Origin completeness: batch kg = sum of its deliveries", Status = Math.Abs(kg - sum) < 0.01 ? "pass" : "fail",
                Detail = $"{kg:N0} = " + string.Join(" + ", rows.Select(o => $"{o.Kg:N0}")) + (Math.Abs(kg - sum) < 0.01 ? "" : $" = {sum:N0}"),
            });
        }

        if (b.Technology == "qvevri")
        {
            var sep = res.Stages.FirstOrDefault(s => s.Type == "separation");
            res.Checks.Add(new LossEngine.Check
            {
                Key = "qvevri_rule", Label = "Qvevri rule (information)", Status = "info", Source = "Challenge Book",
                Detail = "Challenge Book: by law 'qvevri wine' must be fermented and matured in the qvevri up to 31 December of the year of pressing. " +
                         (sep != null ? $"This batch was separated on {Fmt.Date(sep.Date)} — please verify the label category." : "No separation recorded yet — please verify the label category."),
            });
        }
        return res;
    }
}
