using System.Text.Json;
using BacchusLedger.Contracts.Readings;
using BacchusLedger.Extensions;
using Microsoft.Data.Sqlite;

namespace BacchusLedger.Services;

public sealed class ReadingService : IReadingService
{
    public Task<ServiceResult<ReadingListResponse>> ListAsync(long? batch, string? vessel, string? winery)
    {
        using var c = Db.Open();
        long? vid = null;
        if (!string.IsNullOrEmpty(vessel))
        {
            if (Db.VesselRow(c, vessel, winery) is not { } v) return Task.FromResult(ServiceResult<ReadingListResponse>.NotFound("vessel not found"));
            vid = (long)v["id"]!;
        }
        var rows = Db.Query(c, """
            SELECT r.*, b.code AS batch_code, v.code AS vessel_code, v.cellar_label, w.code AS winery
            FROM readings r JOIN batches b ON b.id=r.batch_id JOIN wineries w ON w.id=b.winery_id LEFT JOIN vessels v ON v.id=r.vessel_id
            WHERE ($1 IS NULL OR r.batch_id=$1) AND ($2 IS NULL OR r.vessel_id=$2) ORDER BY r.taken_at, r.id
            """, batch, vid);
        // Initial Brix per vessel/batch: latest entry (corrections are new rows)
        var brix = new Dictionary<string, Dictionary<string, object?>>();
        foreach (var r in rows.Where(r => (string)r["type"]! == "initial_brix")) brix[$"{r["batch_id"]}/{r["vessel_id"]}"] = r;
        var list = rows.Select(r =>
        {
            double? b = brix.TryGetValue($"{r["batch_id"]}/{r["vessel_id"]}", out var br) ? Db.D(br["value"]) : null;
            return new ReadingRowDto((long)r["id"]!, Db.L(r["batch_id"]), Db.L(r["vessel_id"]), r["taken_at"] as string, r["type"] as string, Db.D(r["value"]), r["unit"] as string,
                r["instrument"] as string, r["person"] as string, r["note"] as string, r["source"] as string, r["batch_code"] as string, r["vessel_code"] as string,
                r["cellar_label"] as string, r["winery"] as string, ToDto(Readings.Calc((string)r["type"]!, Db.D(r["value"])!.Value, b)),
                !((r["note"] as string ?? "").StartsWith(Seed.NoTime)));
        }).ToList();
        var starts = brix.Values.Select(r =>
        {
            double v = Db.D(r["value"])!.Value;
            return new InitialBrixDto(Db.L(r["batch_id"]), r["batch_code"] as string, r["vessel_code"] as string, r["cellar_label"] as string, r["taken_at"] as string,
                v, Readings.PotentialAbv(v), Math.Round(Readings.BrixToSg(v), 4), r["source"] as string);
        }).ToList();
        List<InitialSugarDto> sugar = batch == null && vid == null ? [] : Db.Query(c, """
            SELECT l.date, l.sugar_gl, l.source, b.code AS batch_code FROM lab l JOIN batches b ON b.id=l.batch_id
            WHERE l.sugar_gl IS NOT NULL AND ($1 IS NULL OR l.batch_id=$1) AND ($2 IS NULL OR l.vessel_id=$2)
              AND l.date=(SELECT MIN(l2.date) FROM lab l2 WHERE l2.batch_id=l.batch_id AND l2.sugar_gl IS NOT NULL)
            ORDER BY b.code
            """, batch, vid).Select(l => new InitialSugarDto(l["date"] as string, Db.D(l["sugar_gl"]), l["source"] as string, l["batch_code"] as string)).ToList();
        var formulas = new ReadingFormulasDto(Readings.FormulaPotential, Readings.FormulaProgress, Readings.FormulaSg);
        return Task.FromResult<ServiceResult<ReadingListResponse>>(new ReadingListResponse(list, starts, sugar, formulas));
    }

    public Task<ServiceResult<ReadingPreviewResponse>> PreviewAsync(JsonElement body)
    {
        using var c = Db.Open();
        var (items, err) = Inputs(c, body);
        if (items == null) return Task.FromResult(ServiceResult<ReadingPreviewResponse>.BadRequest(err!));
        return Task.FromResult<ServiceResult<ReadingPreviewResponse>>(new ReadingPreviewResponse(items.Select(r => Result(c, r, Readings.Warnings(c, r))).ToList()));
    }

    public Task<ServiceResult<ReadingsSavedResponse>> SaveAsync(JsonElement body)
    {
        string? error = null;
        var saved = Db.Write((c, tx) =>
        {
            var (items, err) = Inputs(c, body);
            if (items == null) { error = err; return []; }
            return items.Select(r => { var (id, w) = Readings.Insert(c, tx, r); return Result(c, r, w, id); }).ToList();
        });
        return Task.FromResult(error != null ? ServiceResult<ReadingsSavedResponse>.BadRequest(error) : ServiceResult<ReadingsSavedResponse>.Ok(new ReadingsSavedResponse(saved)));
    }

    static (List<Readings.Input>? items, string? error) Inputs(SqliteConnection c, JsonElement body)
    {
        if (body.Num("batch_id") is not { } bid || Db.BatchRow(c, (long)bid) is not { } b) return (null, "Choose a batch");
        long? vesselId = null;
        if (body.Str("vessel") is { Length: > 0 } vc)
        {
            var v = Db.VesselRow(c, vc, (string)b["winery_code"]!);
            if (v == null) return (null, $"Vessel {vc} not found at {b["winery_code"]}");
            vesselId = (long)v["id"]!;
        }
        var takenAt = body.Str("taken_at") is { Length: >= 16 } ta ? ta[..16] : DateTime.Now.ToString("yyyy-MM-ddTHH:mm");
        var source = body.Flag("live_demo") ? "Entered (live demo)" : "Entered";
        if (!body.TryGetProperty("readings", out var arr) || arr.ValueKind != JsonValueKind.Array) return (null, "No readings");
        var list = new List<Readings.Input>();
        foreach (var it in arr.EnumerateArray())
        {
            if (it.Num("value") is not { } val) continue; // empty field = not measured
            var r = new Readings.Input((long)bid, vesselId, takenAt, it.Str("type") ?? "", val,
                body.Str("instrument") is { Length: > 0 } ins ? ins : null, body.Str("person"), body.Str("note"), source);
            if (Readings.Validate(r) is { } err) return (null, err);
            list.Add(r);
        }
        return list.Count == 0 ? (null, "Enter a value") : (list, null);
    }

    static ReadingResultDto Result(SqliteConnection c, Readings.Input r, List<string> warnings, long? id = null)
    {
        // ABV entered together with density uses the Initial Brix of the same vessel/batch
        double? brix = r.Type == "initial_brix" ? r.Value : Readings.LatestBrix(c, r.BatchId, r.VesselId);
        return new ReadingResultDto(id, r.Type, r.Value, Readings.Units[r.Type], ToDto(Readings.Calc(r.Type, r.Value, brix)), warnings, r.Source);
    }

    static ReadingCalcDto ToDto(Readings.CalcResult c) => new(c.PotentialAbv, c.Sg, c.ProgressPct, c.Formula);
}
