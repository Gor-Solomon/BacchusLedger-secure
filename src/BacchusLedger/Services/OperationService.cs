using System.Globalization;
using System.Text.Json;
using BacchusLedger.Contracts.Operations;
using BacchusLedger.Extensions;
using BacchusLedger.Services.Mapping;
using Microsoft.Data.Sqlite;

namespace BacchusLedger.Services;

public sealed class OperationService : IOperationService
{
    static readonly string[] Types = ["intake", "separation", "racking", "stock_check", "filtration", "transfer", "bottling", "warehouse"];

    public Task<ServiceResult<OperationPreviewResponse>> PreviewAsync(JsonElement body)
    {
        using var c = Db.Open();
        var (op, err, info, batchId) = Build(c, body);
        if (op == null) return Task.FromResult(ServiceResult<OperationPreviewResponse>.BadRequest(err!));
        var r = LossEngine.Analyze(c, batchId, extra: op);
        var st = r.Stages.First(x => x.OpId == long.MaxValue);
        st.OpId = 0;
        var checks = r.Checks.Where(x => x.Key is "give_out" or "bottle").ToDtos();
        return Task.FromResult<ServiceResult<OperationPreviewResponse>>(
            new OperationPreviewResponse(st.ToDto(), op.Litres, op.Method, info, checks, new OperationTotalsDto(r.TotalLossL, r.LitresNow, r.LossValueUsd)));
    }

    public Task<ServiceResult<OperationSavedResponse>> RecordAsync(JsonElement body)
    {
        long id = 0, batchId = 0; string? error = null;
        Db.Write((c, tx) =>
        {
            var (op, err, _, bid) = Build(c, body);
            if (op == null) { error = err; return 0; }
            batchId = bid;
            id = Db.Insert(c, tx,
                "INSERT INTO operations(batch_id,date,type,from_vessel_id,to_vessel_id,litres,bottles,bottle_ml,remainder_l,method,dip_cm,person,note,source,created_at) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,'Entered',$14)",
                bid, op.Date, op.Type, op.FromId, op.ToId, op.Litres, op.Bottles, op.Ml, op.Remainder, op.Method, op.Dip, op.Person, op.Note, DateTime.UtcNow.ToString("o"));
            return 0;
        });
        if (error != null) return Task.FromResult(ServiceResult<OperationSavedResponse>.BadRequest(error));
        using var c2 = Db.Open();
        var stage = LossEngine.Analyze(c2, batchId).Stages.FirstOrDefault(x => x.OpId == id);
        return Task.FromResult<ServiceResult<OperationSavedResponse>>(new OperationSavedResponse(id, stage?.ToDto()));
    }

    static (LossEngine.Op? op, string? error, DipInfoDto? info, long batchId) Build(SqliteConnection c, JsonElement body)
    {
        string? Str(string k) => body.NonEmptyStr(k);
        double? Num(string k) => body.Num(k);

        var batchIdNum = (long?)Num("batch_id");
        if (batchIdNum == null) return (null, "Choose a batch", null, 0);
        long batchId = batchIdNum.Value;
        var b = Db.BatchRow(c, batchId);
        if (b == null) return (null, "Batch not found", null, 0);
        var winery = (string)b["winery_code"]!;
        var type = Str("type") ?? "";
        if (!Types.Contains(type)) return (null, "Unknown operation type", null, 0);
        var date = Str("date") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return (null, "Date must be YYYY-MM-DD", null, 0);

        Dictionary<string, object?>? from = Str("from_vessel") is { } fc ? Db.VesselRow(c, fc, winery) : null;
        Dictionary<string, object?>? to = Str("to_vessel") is { } tc ? Db.VesselRow(c, tc, winery) : null;
        if (Str("from_vessel") != null && from == null) return (null, $"Vessel {Str("from_vessel")} not found at {winery}", null, 0);
        if (Str("to_vessel") != null && to == null) return (null, $"Vessel {Str("to_vessel")} not found at {winery}", null, 0);

        bool needsFrom = type is "separation" or "racking" or "filtration" or "transfer" or "bottling" or "stock_check";
        bool needsTo = type is "separation" or "racking" or "filtration" or "transfer" or "intake";
        if (needsFrom && from == null) return (null, "Choose the 'from' vessel", null, 0);
        if (needsTo && to == null && type != "intake") return (null, "Choose the 'to' vessel", null, 0);
        if (from != null && to != null && (long)from["id"]! == (long)to["id"]!) return (null, "From and to vessel must differ", null, 0);

        var mode = Str("mode") ?? "litres";
        double? litres = Num("litres"), dip = null, ml = Num("bottle_ml") ?? 750, rem = Num("remainder_l");
        int? bottles = Num("bottles") is { } bt ? (int)Math.Round(bt) : null;
        string method = "paper log";
        DipInfoDto? info = null;

        if (type is "bottling" or "warehouse")
        {
            if (bottles is null or <= 0) return (null, "Enter the bottle count", null, 0);
            method = "bottle count";
            litres = type == "bottling" ? bottles.Value * ml.Value / 1000.0 : null;
        }
        else if (type == "intake")
        {
            method = "scale";
            litres = null;
        }
        else if (mode == "dip")
        {
            dip = Num("dip_cm");
            if (dip == null) return (null, "Enter the dip reading (cm)", null, 0);
            var dv = type == "stock_check" ? from! : to!;
            var m = Calibration.TryParse(dv["calibration_json"] as string);
            if (m == null) return (null, $"{Fmt.Vessel((string)dv["code"]!, dv["cellar_label"] as string)}: Volume unknown — enter litres manually", null, 0);
            if (dip < 0 || dip > m.H) return (null, $"Dip must be between 0 and {m.H} cm for {dv["code"]}", null, 0);
            double total = m.LitresAtDip(dip.Value);
            double already = 0;
            if (type != "stock_check")
            {
                var cur = LossEngine.Analyze(c, batchId, compare: false);
                already = cur.VesselLitres.GetValueOrDefault((string)dv["code"]!);
            }
            litres = Math.Round(total - already, 1);
            method = "dip";
            info = new DipInfoDto((string)dv["code"]!, dip, Math.Round(total, 1), already, dv["source"] as string);
        }
        if (type is not ("intake" or "warehouse") && litres is null or < 0) return (null, "Enter litres (or a dip reading)", null, 0);

        var note = Str("note");
        if (type == "intake" && Num("kg") is { } kg) note = $"{kg:N0} kg grapes" + (note != null ? " · " + note : "");

        return (new LossEngine.Op
        {
            Id = long.MaxValue, Date = date, Type = type,
            FromId = from == null ? null : (long)from["id"]!, ToId = to == null ? null : (long)to["id"]!,
            FromCode = from?["code"] as string, ToCode = to?["code"] as string,
            Litres = litres, Bottles = bottles, Ml = type is "bottling" or "warehouse" ? ml : null, Remainder = rem, Dip = dip,
            Method = method, Note = note, Person = Str("person"), Source = "Entered",
        }, null, info, batchId);
    }
}
