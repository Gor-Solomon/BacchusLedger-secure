using System.Text.Json;
using BacchusLedger.Contracts.Readings;
using BacchusLedger.Dataset;
using BacchusLedger.Extensions;
using BacchusLedger.Models;
using BacchusLedger.Repositories;
using BacchusLedger.Domain.Readings;
using BacchusLedger.Domain.Common;

namespace BacchusLedger.Services;

public sealed class ReadingService(
    IReadingRepository readings, ILabRepository lab, IBatchRepository batches, IVesselRepository vessels, IUnitOfWork unitOfWork) : IReadingService
{
    public async Task<ServiceResult<ReadingListResponse>> ListAsync(long? batch, string? vessel, string? winery)
    {
        long? vid = null;
        if (!string.IsNullOrEmpty(vessel))
        {
            if (await vessels.FindAsync(vessel, winery) is not { } v) return ServiceResult<ReadingListResponse>.NotFound("vessel not found");
            vid = v.Id;
        }
        var rows = await readings.ListAsync(batch, vid);
        // Initial Brix per vessel/batch: latest entry (corrections are new rows)
        var brix = new Dictionary<string, ReadingRecord>();
        foreach (var r in rows.Where(r => r.Type == "initial_brix")) brix[$"{r.BatchId}/{r.VesselId}"] = r;
        var list = rows.Select(r =>
        {
            double? b = brix.TryGetValue($"{r.BatchId}/{r.VesselId}", out var br) ? br.Value : null;
            return new ReadingRowDto(r.Id, r.BatchId, r.VesselId, r.TakenAt, r.Type, r.Value, r.Unit, r.Instrument, r.Person, r.Note, r.Source, r.BatchCode, r.VesselCode,
                r.CellarLabel, r.Winery, ToDto(calculator.Calculate(r.Type, r.Value, b)), !(r.Note ?? "").StartsWith(SeedDataset.NoTime));
        }).ToList();
        var starts = brix.Values.Select(r => new InitialBrixDto(r.BatchId, r.BatchCode, r.VesselCode, r.CellarLabel, r.TakenAt, r.Value,
            new InitialBrix(r.Value).PotentialAbv, Math.Round(new InitialBrix(r.Value).SpecificGravity, 4), r.Source)).ToList();
        List<InitialSugarDto> sugar = batch == null && vid == null ? [] : await lab.InitialSugarAsync(batch, vid);
        return new ReadingListResponse(list, starts, sugar, new ReadingFormulasDto(ReadingTypes.FormulaPotential, ReadingTypes.FormulaProgress, ReadingTypes.FormulaSg));
    }

    public async Task<ServiceResult<ReadingPreviewResponse>> PreviewAsync(JsonElement body)
    {
        var (items, err) = await InputsAsync(body);
        if (items == null) return ServiceResult<ReadingPreviewResponse>.BadRequest(err!);
        var results = new List<ReadingResultDto>();
        foreach (var r in items) results.Add(await ResultAsync(r, await WarningsAsync(r)));
        return new ReadingPreviewResponse(results);
    }

    public async Task<ServiceResult<ReadingsSavedResponse>> SaveAsync(JsonElement body)
    {
        var (saved, error) = await unitOfWork.WriteAsync(async () =>
        {
            var (items, err) = await InputsAsync(body);
            if (items == null) return (new List<ReadingResultDto>(), err);
            var list = new List<ReadingResultDto>();
            foreach (var r in items)
            {
                var (id, w) = await InsertAsync(r);
                list.Add(await ResultAsync(r, w, id));
            }
            return (list, (string?)null);
        });
        return error != null ? ServiceResult<ReadingsSavedResponse>.BadRequest(error) : new ReadingsSavedResponse(saved);
    }

    /// Saves a reading (append-only) and returns the warnings shown for it.
    public async Task<(long id, List<string> warnings)> InsertAsync(ReadingInput r)
    {
        var warnings = await WarningsAsync(r);
        var id = await readings.InsertAsync(new NewReading(r.BatchId, r.VesselId, r.TakenAt, r.Type, r.Value, ReadingTypes.Units[r.Type],
            r.Instrument ?? ReadingTypes.DefaultInstrument[r.Type], r.Person, r.Note, r.Source));
        return (id, warnings);
    }

    /// Warnings shown before and after saving (the reading is still saved: corrections are new rows).
    public async Task<List<string>> WarningsAsync(ReadingInput r)
    {
        var w = new List<string>();
        if (r.Type == "initial_brix")
        {
            // First fermentation reading of this vessel/batch: any non-Brix reading, or a lab row (e.g. the Dataset A3 fermentation log)
            if (await readings.FirstFermentationAsync(r.BatchId, r.VesselId, r.TakenAt) is { } first)
                w.Add($"Fermentation has already started in this vessel (first reading {first.ToDisplayDateTime()}). Refractometers are not accurate once alcohol is present — use the hydrometer. Initial Brix should be taken before fermentation.");
            if (await readings.HasInitialBrixAsync(r.BatchId, r.VesselId))
                w.Add("Initial Brix is recorded once per vessel/batch. This entry is saved as a correction — the latest Initial Brix is used for the calculations.");
        }
        if (r.Type == "hydrometer_abv" && await readings.LatestBrixAsync(r.BatchId, r.VesselId) == null)
            w.Add("No Initial Brix for this vessel/batch yet — fermentation progress cannot be calculated.");
        return w;
    }

    async Task<(List<ReadingInput>? items, string? error)> InputsAsync(JsonElement body)
    {
        if (body.Num("batch_id") is not { } bid || await batches.GetAsync((long)bid) is not { } b) return (null, "Choose a batch");
        long? vesselId = null;
        if (body.Str("vessel") is { Length: > 0 } vc)
        {
            if (await vessels.FindAsync(vc, b.WineryCode) is not { } v) return (null, $"Vessel {vc} not found at {b.WineryCode}");
            vesselId = v.Id;
        }
        var takenAt = body.Str("taken_at") is { Length: >= 16 } ta ? ta[..16] : DateTime.Now.ToString("yyyy-MM-ddTHH:mm");
        var source = body.Flag("live_demo") ? "Entered (live demo)" : "Entered";
        if (!body.TryGetProperty("readings", out var arr) || arr.ValueKind != JsonValueKind.Array) return (null, "No readings");
        var list = new List<ReadingInput>();
        foreach (var it in arr.EnumerateArray())
        {
            if (it.Num("value") is not { } val) continue; // empty field = not measured
            var r = new ReadingInput((long)bid, vesselId, takenAt, it.Str("type") ?? "", val,
                body.Str("instrument") is { Length: > 0 } ins ? ins : null, body.Str("person"), body.Str("note"), source);
            if (new ReadingValidator().Validate(r) is { } err) return (null, err);
            list.Add(r);
        }
        return list.Count == 0 ? (null, "Enter a value") : (list, null);
    }

    async Task<ReadingResultDto> ResultAsync(ReadingInput r, List<string> warnings, long? id = null)
    {
        // ABV entered together with density uses the Initial Brix of the same vessel/batch
        double? brix = r.Type == "initial_brix" ? r.Value : await readings.LatestBrixAsync(r.BatchId, r.VesselId);
        return new ReadingResultDto(id, r.Type, r.Value, ReadingTypes.Units[r.Type], ToDto(calculator.Calculate(r.Type, r.Value, brix)), warnings, r.Source);
    }

    readonly ReadingCalculator calculator = new();

    static ReadingCalcDto ToDto(ReadingCalculation c) => new(c.PotentialAbv, c.Sg, c.ProgressPct, c.Formula);
}
