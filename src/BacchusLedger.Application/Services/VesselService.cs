using System.Text.Json;
using System.Text.Json.Nodes;
using BacchusLedger.Application.Contracts.Common;
using BacchusLedger.Application.Contracts.Vessels;
using BacchusLedger.Application.Extensions;
using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Domain.Vessels;

namespace BacchusLedger.Application.Services;

public sealed class VesselService(
    IVesselRepository vessels, IBatchRepository batches, ISettingsRepository settings, ILossAnalysisService losses, IUnitOfWork unitOfWork) : IVesselService
{
    public async Task<IReadOnlyList<VesselDto>> ListAsync()
    {
        var contents = await ContentsAsync();
        return (await vessels.ListAsync()).Select(v => ToDto(v, contents)).ToList();
    }

    public async Task<ServiceResult<VesselDetailResponse>> GetAsync(string code, string? winery)
    {
        if (await vessels.FindAsync(code, winery) is not { } v) return ServiceResult<VesselDetailResponse>.NotFound("vessel not found");
        var m = VesselCalibration.TryParse(v.CalibrationJson);
        // Inner shape for drawing: [z_cm, radius_cm]
        List<double[]>? profile = null;
        if (m?.Kind == "qvevri")
        {
            var t = JsonNode.Parse(v.CalibrationJson!)!["table"]!.AsArray();
            profile = t.Where((r, i) => (double)r![0]! % 5 == 0 || i == t.Count - 1).Select(r => new[] { (double)r![0]!, (double)r![1]! }).ToList();
        }
        else if (m != null) profile = [[0, m.DiameterCm / 2], [m.HeightCm, m.DiameterCm / 2]];
        var used = await vessels.ListBatchesUsingAsync(v.Id);
        var chart = m == null ? [] : m.DipChart(5).Select(p => new DipChartRowDto(p.DipCm, p.FillCm, p.Litres)).ToList();
        return new VesselDetailResponse(ToDto(v, await ContentsAsync()), chart, profile, used);
    }

    public async Task<ServiceResult<object>> ConvertAsync(string code, double? dip, double? litres, string? winery)
    {
        if (await vessels.FindAsync(code, winery) is not { } v) return ServiceResult<object>.NotFound("vessel not found");
        var m = VesselCalibration.TryParse(v.CalibrationJson);
        if (m == null) return ServiceResult<object>.BadRequest("Volume unknown — enter litres manually");
        if (dip is { } d)
        {
            if (d < 0 || d > m.HeightCm) return ServiceResult<object>.BadRequest($"dip must be 0–{m.HeightCm} cm");
            return ServiceResult<object>.Ok(new DipToLitresResponse(code, d, m.HeightCm - d, Math.Round(m.LitresAtDip(d), 1), Math.Round(m.Capacity, 1), "Calculated"));
        }
        if (litres is { } l) return ServiceResult<object>.Ok(new LitresToDipResponse(code, l, Math.Round(m.DipForLitres(l), 1), Math.Round(m.Capacity, 1), "Calculated"));
        return ServiceResult<object>.BadRequest("pass ?dip= or ?litres=");
    }

    public async Task<ServiceResult<VesselCreatedResponse>> CreateAsync(JsonElement body)
    {
        var winery = body.Str("winery"); var code = body.Str("code"); var type = body.Str("type") ?? "qvevri";
        if (string.IsNullOrEmpty(winery) || string.IsNullOrEmpty(code)) return ServiceResult<VesselCreatedResponse>.BadRequest("Winery and code are required");
        if (type is not ("tank" or "qvevri")) return ServiceResult<VesselCreatedResponse>.BadRequest("Type must be tank or qvevri");
        var src = body.Flag("live_demo") ? "Entered (live demo)" : "Entered";
        double? depth = body.Num("depth_cm"), neck = body.Num("neck_d_cm"), max = body.Num("max_d_cm"), known = body.Num("known_volume_l");
        double? dCm = body.Num("d_cm"), hCm = body.Num("h_cm");
        if (new[] { depth, neck, max, known, dCm, hCm }.Any(x => x is <= 0)) return ServiceResult<VesselCreatedResponse>.BadRequest("Measurements must be positive (or empty)");
        try
        {
            return await unitOfWork.WriteAsync(async () =>
            {
                if (await vessels.ExistsAsync(winery, code)) return ServiceResult<VesselCreatedResponse>.BadRequest($"Vessel {code} already exists at {winery}");
                string? json = null; double? cap = known;
                if (type == "qvevri") (json, cap, _) = QvevriCalibration(null, null, null, depth, neck, max, known);
                else if (dCm != null && hCm != null) { json = new CylinderTank(dCm.Value, hCm.Value).ToCalibrationJson(); cap = Math.Round(new CylinderTank(dCm.Value, hCm.Value).Capacity, 1); }
                var label = body.Str("cellar_label");
                var name = body.Str("name") is { Length: > 0 } n ? n : (type == "qvevri" ? $"Qvevri {code}" : $"Tank {code}");
                var id = await vessels.InsertAsync(new NewVessel(winery, code, type, name, json, cap, src,
                    string.IsNullOrEmpty(label) ? null : label, string.IsNullOrEmpty(label) ? null : src, depth, neck, max, known));
                return ServiceResult<VesselCreatedResponse>.Ok(new VesselCreatedResponse(id, code, winery));
            });
        }
        catch (RepositoryException e) { return ServiceResult<VesselCreatedResponse>.BadRequest(e.Message); }
    }

    public Task<ServiceResult<OkResponse>> UpdateDetailsAsync(string code, string? winery, JsonElement body) => unitOfWork.WriteAsync(async () =>
    {
        if (await vessels.FindAsync(code, winery) is not { } v) return ServiceResult<OkResponse>.NotFound("vessel not found");
        var src = body.Flag("live_demo") ? "Entered (live demo)" : "Entered";
        if (body.Has("cellar_label"))
        {
            var label = body.Str("cellar_label");
            if (!string.IsNullOrEmpty(label) && await vessels.LabelUsedAsync(v.WineryId, v.Id, label))
                return ServiceResult<OkResponse>.BadRequest($"Cellar label {label} is already used by another vessel at {v.WineryCode}");
            await vessels.SetLabelAsync(v.Id, string.IsNullOrEmpty(label) ? null : label, string.IsNullOrEmpty(label) ? null : src);
        }
        string[] meas = ["depth_cm", "neck_d_cm", "max_d_cm", "known_volume_l"];
        if (v.Type == "qvevri" && meas.Any(k => body.Has(k)))
        {
            double? depth = body.Num("depth_cm"), neck = body.Num("neck_d_cm"), max = body.Num("max_d_cm"), known = body.Num("known_volume_l");
            if (new[] { depth, neck, max, known }.Any(x => x is <= 0)) return ServiceResult<OkResponse>.BadRequest("Measurements must be positive (or empty)");
            var (json, cap, csrc) = QvevriCalibration(v.CalibrationJson, v.Source, v.CapacityL, depth, neck, max, known);
            await vessels.SetMeasurementsAsync(v.Id, new VesselMeasurements(depth, neck, max, known, json, cap, v.Source == "Entered (live demo)" ? v.Source : csrc));
        }
        return ServiceResult<OkResponse>.Ok(new OkResponse(true));
    });

    /// Litres per vessel across all batches (replay of each batch).
    async Task<Dictionary<string, List<VesselContentDto>>> ContentsAsync()
    {
        var s = await settings.GetAsync();
        var d = new Dictionary<string, List<VesselContentDto>>();
        var refs = await batches.ListRefsAsync();
        var results = await losses.AnalyzeManyAsync(refs.Select(b => b.Id).ToList(), s);
        foreach (var b in refs)
        {
            var r = results[b.Id];
            foreach (var (v, l) in r.VesselLitres)
            {
                var key = $"{b.WineryCode}/{v}";
                if (!d.TryGetValue(key, out var list)) d[key] = list = [];
                list.Add(new VesselContentDto(b.Code, b.Id, l, b.IsCopy));
            }
        }
        return d;
    }

    static VesselDto ToDto(VesselRecord v, Dictionary<string, List<VesselContentDto>> contents)
    {
        var m = VesselCalibration.TryParse(v.CalibrationJson);
        var cj = m == null ? null : JsonNode.Parse(v.CalibrationJson!)!;
        var list = contents.TryGetValue($"{v.WineryCode}/{v.Code}", out var l) ? l : [];
        var model = (string?)cj?["model"];
        return new VesselDto(v.Id, v.WineryCode, v.Code, v.Type, v.Name, v.CapacityL, v.Source, v.CellarLabel, v.CellarLabelSource, v.DepthCm, v.NeckDCm, v.MaxDCm,
            v.KnownVolumeL, m != null, m?.Kind ?? "none", model, CalibrationNote(v, m, cj),
            m == null ? (v.CapacityL != null ? "Entered" : null) : m.Kind == "cylinder" ? v.Source : model == "measured" ? "Calculated" : "Dataset A5",
            m?.Kind == "cylinder" ? m.DiameterCm : null, m?.HeightCm,
            m?.Kind == "qvevri" ? (double?)cj!["raw_total_l"] : null, m?.Kind == "qvevri" ? (double?)cj!["scale"] : null,
            list, list.Sum(x => x.Litres));
    }

    static string CalibrationNote(VesselRecord v, VesselCalibration? m, JsonNode? cj)
    {
        var known = v.KnownVolumeL;
        if (m == null)
            return known != null ? $"Known volume {known:N0} L (Entered). No measurements — dip → litres is not available; enter litres manually." : "Volume unknown — enter litres manually";
        if (m.Kind == "cylinder")
            return $"Cylinder Ø{m.DiameterCm:0.#} × {m.HeightCm:0.#} cm" + (v.Source == "DEMO ASSUMPTION" ? " — size is a DEMO ASSUMPTION" : " (Entered)");
        if ((string?)cj?["model"] == "measured")
            return $"A5 shape scaled to the entered measurements (depth {m.HeightCm:0.#} cm, neck Ø{v.NeckDCm:0.#}, max Ø{v.MaxDCm:0.#})" +
                   (known != null ? $", radii scaled to the known volume {known:N0} L" : "");
        return "generalised A5 model (demo)" + (known != null ? $" — known volume {known:N0} L entered; add depth and diameters to use your own shape" : "");
    }

    /// Optional qvevri measurements: depth + neck Ø + max Ø → own calibration (A5 shape scaled to them, and to the known volume if given).
    /// Otherwise no guess: no calibration, capacity = known volume (or unknown). The demo A5 qvevri keep their A5 model when no measurements are entered.
    static (string? json, double? capacity, string source) QvevriCalibration(string? currentJson, string? currentSource, double? currentCapacity,
        double? depth, double? neck, double? max, double? known)
    {
        if (depth is > 0 && neck is > 0 && max is > 0)
        {
            var j = new QvevriProfile(depth.Value, neck.Value, max.Value).ToCalibrationJson(known, "measured");
            return (j, Math.Round(VesselCalibration.Parse(j).Capacity, 1), "Entered");
        }
        if (currentJson != null && (string?)JsonNode.Parse(currentJson)?["model"] == "A5" && currentSource == "Dataset A5") return (currentJson, currentCapacity, "Dataset A5");
        return (null, known, "Entered");
    }
}
