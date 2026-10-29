using System.Globalization;
using System.Text.Json;
using BacchusLedger.Contracts.Operations;
using BacchusLedger.Extensions;
using BacchusLedger.Models;
using BacchusLedger.Repositories;
using BacchusLedger.Services.Mapping;
using BacchusLedger.Domain.Vessels;

namespace BacchusLedger.Services;

public sealed class OperationService(
    IBatchRepository batches, IVesselRepository vessels, IOperationRepository operations, ILossAnalysisService losses, IUnitOfWork unitOfWork) : IOperationService
{
    static readonly string[] Types = ["intake", "separation", "racking", "stock_check", "filtration", "transfer", "bottling", "warehouse"];

    public async Task<ServiceResult<OperationPreviewResponse>> PreviewAsync(JsonElement body)
    {
        var (op, err, info, batchId) = await BuildAsync(body);
        if (op == null) return ServiceResult<OperationPreviewResponse>.BadRequest(err!);
        var r = await losses.AnalyzeAsync(batchId, extra: op);
        var st = r.Stages.First(x => x.OpId == long.MaxValue);
        st.OpId = 0;
        var checks = r.Checks.Where(x => x.Key is "give_out" or "bottle").ToDtos();
        return new OperationPreviewResponse(st.ToDto(), op.Litres, op.Method, info, checks, new OperationTotalsDto(r.TotalLossL, r.LitresNow, r.LossValueUsd));
    }

    public async Task<ServiceResult<OperationSavedResponse>> RecordAsync(JsonElement body)
    {
        var (id, batchId, error) = await unitOfWork.WriteAsync(async () =>
        {
            var (op, err, _, bid) = await BuildAsync(body);
            if (op == null) return (0L, 0L, err);
            var newId = await operations.InsertAsync(new NewOperation(bid, op.Date, op.Type, op.FromId, op.ToId, op.Litres, op.Bottles, op.Ml, op.Remainder,
                op.Method, op.Dip, op.Person, op.Note, "Entered", DateTime.UtcNow.ToString("o")));
            return (newId, bid, (string?)null);
        });
        if (error != null) return ServiceResult<OperationSavedResponse>.BadRequest(error);
        var stage = (await losses.AnalyzeAsync(batchId)).Stages.FirstOrDefault(x => x.OpId == id);
        return new OperationSavedResponse(id, stage?.ToDto());
    }

    async Task<(LossEngine.Op? op, string? error, DipInfoDto? info, long batchId)> BuildAsync(JsonElement body)
    {
        string? Str(string k) => body.NonEmptyStr(k);
        double? Num(string k) => body.Num(k);

        if ((long?)Num("batch_id") is not { } batchId) return (null, "Choose a batch", null, 0);
        if (await batches.GetAsync(batchId) is not { } b) return (null, "Batch not found", null, 0);
        var winery = b.WineryCode;
        var type = Str("type") ?? "";
        if (!Types.Contains(type)) return (null, "Unknown operation type", null, 0);
        var date = Str("date") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return (null, "Date must be YYYY-MM-DD", null, 0);

        var from = Str("from_vessel") is { } fc ? await vessels.FindAsync(fc, winery) : null;
        var to = Str("to_vessel") is { } tc ? await vessels.FindAsync(tc, winery) : null;
        if (Str("from_vessel") != null && from == null) return (null, $"Vessel {Str("from_vessel")} not found at {winery}", null, 0);
        if (Str("to_vessel") != null && to == null) return (null, $"Vessel {Str("to_vessel")} not found at {winery}", null, 0);

        bool needsFrom = type is "separation" or "racking" or "filtration" or "transfer" or "bottling" or "stock_check";
        bool needsTo = type is "separation" or "racking" or "filtration" or "transfer" or "intake";
        if (needsFrom && from == null) return (null, "Choose the 'from' vessel", null, 0);
        if (needsTo && to == null && type != "intake") return (null, "Choose the 'to' vessel", null, 0);
        if (from != null && to != null && from.Id == to.Id) return (null, "From and to vessel must differ", null, 0);

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
            var m = VesselCalibration.TryParse(dv.CalibrationJson);
            if (m == null) return (null, $"{Fmt.Vessel(dv.Code, dv.CellarLabel)}: Volume unknown — enter litres manually", null, 0);
            if (dip < 0 || dip > m.HeightCm) return (null, $"Dip must be between 0 and {m.HeightCm} cm for {dv.Code}", null, 0);
            double total = m.LitresAtDip(dip.Value);
            double already = 0;
            if (type != "stock_check") already = (await losses.AnalyzeAsync(batchId, compare: false)).VesselLitres.GetValueOrDefault(dv.Code);
            litres = Math.Round(total - already, 1);
            method = "dip";
            info = new DipInfoDto(dv.Code, dip, Math.Round(total, 1), already, dv.Source);
        }
        if (type is not ("intake" or "warehouse") && litres is null or < 0) return (null, "Enter litres (or a dip reading)", null, 0);

        var note = Str("note");
        if (type == "intake" && Num("kg") is { } kg) note = $"{kg:N0} kg grapes" + (note != null ? " · " + note : "");

        return (new LossEngine.Op
        {
            Id = long.MaxValue, Date = date, Type = type, FromId = from?.Id, ToId = to?.Id, FromCode = from?.Code, ToCode = to?.Code,
            Litres = litres, Bottles = bottles, Ml = type is "bottling" or "warehouse" ? ml : null, Remainder = rem, Dip = dip,
            Method = method, Note = note, Person = Str("person"), Source = "Entered",
        }, null, info, batchId);
    }
}
