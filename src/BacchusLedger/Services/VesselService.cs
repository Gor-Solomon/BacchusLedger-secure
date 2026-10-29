using System.Text.Json;
using System.Text.Json.Nodes;
using BacchusLedger.Contracts.Common;
using BacchusLedger.Contracts.Vessels;
using BacchusLedger.Extensions;
using Microsoft.Data.Sqlite;

namespace BacchusLedger.Services;

public sealed class VesselService : IVesselService
{
    public Task<IReadOnlyList<VesselDto>> ListAsync()
    {
        using var c = Db.Open();
        var contents = Contents(c, Db.Settings());
        IReadOnlyList<VesselDto> list = Db.Query(c, "SELECT v.*, w.code AS winery_code FROM vessels v JOIN wineries w ON w.id=v.winery_id ORDER BY w.code, v.id")
            .Select(v => ToDto(v, contents)).ToList();
        return Task.FromResult(list);
    }

    public Task<ServiceResult<VesselDetailResponse>> GetAsync(string code, string? winery)
    {
        using var c = Db.Open();
        var v = Db.VesselRow(c, code, winery);
        if (v == null) return Task.FromResult(ServiceResult<VesselDetailResponse>.NotFound("vessel not found"));
        var m = Calibration.TryParse(v["calibration_json"] as string);
        List<double[]>? profile = null;
        if (m?.Kind == "qvevri")
        {
            var t = JsonNode.Parse((string)v["calibration_json"]!)!["table"]!.AsArray();
            profile = t.Where((r, i) => (double)r![0]! % 5 == 0 || i == t.Count - 1).Select(r => new[] { (double)r![0]!, (double)r![1]! }).ToList();
        }
        else if (m != null) profile = [[0, m.D / 2], [m.H, m.D / 2]];
        // Batches that used this vessel, most recent first (default batch for a reading)
        var batches = Db.Query(c, """
            SELECT b.id, b.code, MAX(x.d) AS last FROM batches b JOIN (
              SELECT batch_id, date AS d FROM operations WHERE from_vessel_id=$1 OR to_vessel_id=$1
              UNION ALL SELECT batch_id, taken_at FROM readings WHERE vessel_id=$1
              UNION ALL SELECT batch_id, date FROM lab WHERE vessel_id=$1) x ON x.batch_id=b.id
            GROUP BY b.id ORDER BY last DESC, (b.source_note LIKE '%copy%'), b.id
            """, v["id"]).Select(b => new VesselBatchDto((long)b["id"]!, b["code"] as string, b["last"] as string)).ToList();
        var chart = m == null ? [] : Calibration.DipChart(m, 5).Select(p => new DipChartRowDto(p.DipCm, p.FillCm, p.Litres)).ToList();
        return Task.FromResult<ServiceResult<VesselDetailResponse>>(new VesselDetailResponse(ToDto(v, Contents(c, Db.Settings())), chart, profile, batches));
    }

    public Task<ServiceResult<object>> ConvertAsync(string code, double? dip, double? litres, string? winery)
    {
        using var c = Db.Open();
        var v = Db.VesselRow(c, code, winery);
        if (v == null) return Task.FromResult(ServiceResult<object>.NotFound("vessel not found"));
        var m = Calibration.TryParse(v["calibration_json"] as string);
        if (m == null) return Task.FromResult(ServiceResult<object>.BadRequest("Volume unknown — enter litres manually"));
        if (dip is { } d)
        {
            if (d < 0 || d > m.H) return Task.FromResult(ServiceResult<object>.BadRequest($"dip must be 0–{m.H} cm"));
            return Task.FromResult(ServiceResult<object>.Ok(new DipToLitresResponse(code, d, m.H - d, Math.Round(m.LitresAtDip(d), 1), Math.Round(m.Capacity, 1), "Calculated")));
        }
        if (litres is { } l) return Task.FromResult(ServiceResult<object>.Ok(new LitresToDipResponse(code, l, Math.Round(m.DipForLitres(l), 1), Math.Round(m.Capacity, 1), "Calculated")));
        return Task.FromResult(ServiceResult<object>.BadRequest("pass ?dip= or ?litres="));
    }

    public Task<ServiceResult<VesselCreatedResponse>> CreateAsync(JsonElement body)
    {
        var winery = body.Str("winery"); var code = body.Str("code"); var type = body.Str("type") ?? "qvevri";
        if (string.IsNullOrEmpty(winery) || string.IsNullOrEmpty(code)) return Bad("Winery and code are required");
        if (type is not ("tank" or "qvevri")) return Bad("Type must be tank or qvevri");
        var src = body.Flag("live_demo") ? "Entered (live demo)" : "Entered";
        double? depth = body.Num("depth_cm"), neck = body.Num("neck_d_cm"), max = body.Num("max_d_cm"), known = body.Num("known_volume_l");
        double? dCm = body.Num("d_cm"), hCm = body.Num("h_cm");
        if (new[] { depth, neck, max, known, dCm, hCm }.Any(x => x is <= 0)) return Bad("Measurements must be positive (or empty)");
        try
        {
            return Task.FromResult(Db.Write((c, tx) =>
            {
                if (Db.Query(c, "SELECT 1 FROM vessels v JOIN wineries w ON w.id=v.winery_id WHERE w.code=$1 AND v.code=$2", winery, code).Count > 0)
                    return ServiceResult<VesselCreatedResponse>.BadRequest($"Vessel {code} already exists at {winery}");
                string? json = null; double? cap = known;
                if (type == "qvevri") (json, cap, _) = QvevriCalibration(new() { ["calibration_json"] = null, ["source"] = src, ["capacity_l"] = null }, depth, neck, max, known);
                else if (dCm != null && hCm != null) { json = Calibration.CylinderJson(dCm.Value, hCm.Value); cap = Math.Round(Calibration.CylinderLitres(dCm.Value, hCm.Value), 1); }
                var label = body.Str("cellar_label");
                var id = Db.Insert(c, tx, """
                    INSERT INTO vessels(winery_id,code,type,name,calibration_json,capacity_l,source,cellar_label,cellar_label_source,depth_cm,neck_d_cm,max_d_cm,known_volume_l)
                    VALUES((SELECT id FROM wineries WHERE code=$1),$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)
                    """, winery, code, type, body.Str("name") is { Length: > 0 } n ? n : (type == "qvevri" ? $"Qvevri {code}" : $"Tank {code}"), json, cap, src,
                    string.IsNullOrEmpty(label) ? null : label, string.IsNullOrEmpty(label) ? null : src, depth, neck, max, known);
                return ServiceResult<VesselCreatedResponse>.Ok(new VesselCreatedResponse(id, code, winery));
            }));
        }
        catch (SqliteException e) { return Bad(e.Message); }

        static Task<ServiceResult<VesselCreatedResponse>> Bad(string e) => Task.FromResult(ServiceResult<VesselCreatedResponse>.BadRequest(e));
    }

    public Task<ServiceResult<OkResponse>> UpdateDetailsAsync(string code, string? winery, JsonElement body) => Task.FromResult(Db.Write((c, tx) =>
    {
        var v = Db.VesselRow(c, code, winery);
        if (v == null) return ServiceResult<OkResponse>.NotFound("vessel not found");
        var src = body.Flag("live_demo") ? "Entered (live demo)" : "Entered";
        if (body.Has("cellar_label"))
        {
            var label = body.Str("cellar_label");
            if (!string.IsNullOrEmpty(label) && Db.Query(c, "SELECT 1 FROM vessels WHERE winery_id=$1 AND id<>$2 AND lower(trim(cellar_label))=lower($3)", v["winery_id"], v["id"], label).Count > 0)
                return ServiceResult<OkResponse>.BadRequest($"Cellar label {label} is already used by another vessel at {v["winery_code"]}");
            Db.Exec(c, tx, "UPDATE vessels SET cellar_label=$1, cellar_label_source=$2 WHERE id=$3",
                string.IsNullOrEmpty(label) ? null : label, string.IsNullOrEmpty(label) ? null : src, v["id"]);
        }
        string[] meas = ["depth_cm", "neck_d_cm", "max_d_cm", "known_volume_l"];
        if ((string)v["type"]! == "qvevri" && meas.Any(k => body.Has(k)))
        {
            double? depth = body.Num("depth_cm"), neck = body.Num("neck_d_cm"), max = body.Num("max_d_cm"), known = body.Num("known_volume_l");
            if (new[] { depth, neck, max, known }.Any(x => x is <= 0)) return ServiceResult<OkResponse>.BadRequest("Measurements must be positive (or empty)");
            var (json, cap, csrc) = QvevriCalibration(v, depth, neck, max, known);
            Db.Exec(c, tx, "UPDATE vessels SET depth_cm=$1, neck_d_cm=$2, max_d_cm=$3, known_volume_l=$4, calibration_json=$5, capacity_l=$6, source=$7 WHERE id=$8",
                depth, neck, max, known, json, cap, (string?)v["source"] == "Entered (live demo)" ? v["source"] : csrc, v["id"]);
        }
        return ServiceResult<OkResponse>.Ok(new OkResponse(true));
    }));

    /// Litres per vessel across all batches (replay of each batch).
    static Dictionary<string, List<VesselContentDto>> Contents(SqliteConnection c, Dictionary<string, string> s)
    {
        var d = new Dictionary<string, List<VesselContentDto>>();
        foreach (var b in Db.Query(c, "SELECT b.id, b.code, b.source_note, w.code AS winery FROM batches b JOIN wineries w ON w.id=b.winery_id"))
        {
            var r = LossEngine.Analyze(c, (long)b["id"]!, s, compare: false);
            foreach (var (v, l) in r.VesselLitres)
            {
                var key = $"{b["winery"]}/{v}";
                if (!d.TryGetValue(key, out var list)) d[key] = list = [];
                list.Add(new VesselContentDto(b["code"] as string, (long)b["id"]!, l, Db.IsCopy(b)));
            }
        }
        return d;
    }

    static VesselDto ToDto(Dictionary<string, object?> v, Dictionary<string, List<VesselContentDto>> contents)
    {
        var m = Calibration.TryParse(v["calibration_json"] as string);
        var cj = m == null ? null : JsonNode.Parse((string)v["calibration_json"]!)!;
        var list = contents.TryGetValue($"{v["winery_code"]}/{v["code"]}", out var l) ? l : [];
        var model = (string?)cj?["model"];
        return new VesselDto((long)v["id"]!, v["winery_code"] as string, v["code"] as string, v["type"] as string, v["name"] as string, Db.D(v["capacity_l"]), v["source"] as string,
            v["cellar_label"] as string, v["cellar_label_source"] as string, Db.D(v["depth_cm"]), Db.D(v["neck_d_cm"]), Db.D(v["max_d_cm"]), Db.D(v["known_volume_l"]),
            m != null, m?.Kind ?? "none", model, CalibrationNote(v, m, cj),
            m == null ? (v["capacity_l"] != null ? "Entered" : null) : m.Kind == "cylinder" ? (string?)v["source"] : model == "measured" ? "Calculated" : "Dataset A5",
            m?.Kind == "cylinder" ? m.D : null, m?.H,
            m?.Kind == "qvevri" ? (double?)cj!["raw_total_l"] : null, m?.Kind == "qvevri" ? (double?)cj!["scale"] : null,
            list, list.Sum(x => x.Litres));
    }

    static string CalibrationNote(Dictionary<string, object?> v, Calibration.Model? m, JsonNode? cj)
    {
        var known = Db.D(v["known_volume_l"]);
        if (m == null)
            return known != null ? $"Known volume {known:N0} L (Entered). No measurements — dip → litres is not available; enter litres manually." : "Volume unknown — enter litres manually";
        if (m.Kind == "cylinder")
            return $"Cylinder Ø{m.D:0.#} × {m.H:0.#} cm" + ((string?)v["source"] == "DEMO ASSUMPTION" ? " — size is a DEMO ASSUMPTION" : " (Entered)");
        if ((string?)cj?["model"] == "measured")
            return $"A5 shape scaled to the entered measurements (depth {m.H:0.#} cm, neck Ø{Db.D(v["neck_d_cm"]):0.#}, max Ø{Db.D(v["max_d_cm"]):0.#})" +
                   (known != null ? $", radii scaled to the known volume {known:N0} L" : "");
        return "generalised A5 model (demo)" + (known != null ? $" — known volume {known:N0} L entered; add depth and diameters to use your own shape" : "");
    }

    /// Optional qvevri measurements: depth + neck Ø + max Ø → own calibration (A5 shape scaled to them, and to the known volume if given).
    /// Otherwise no guess: no calibration, capacity = known volume (or unknown). The demo A5 qvevri keep their A5 model when no measurements are entered.
    static (string? json, double? capacity, string source) QvevriCalibration(Dictionary<string, object?> v, double? depth, double? neck, double? max, double? known)
    {
        if (depth is > 0 && neck is > 0 && max is > 0)
        {
            var j = Calibration.QvevriJson(depth.Value, neck.Value, max.Value, known, "measured");
            return (j, Math.Round(Calibration.Parse(j).Capacity, 1), "Entered");
        }
        var cur = v["calibration_json"] as string;
        if (cur != null && (string?)JsonNode.Parse(cur)?["model"] == "A5" && (string?)v["source"] == "Dataset A5") return (cur, Db.D(v["capacity_l"]), "Dataset A5");
        return (null, known, "Entered");
    }
}
