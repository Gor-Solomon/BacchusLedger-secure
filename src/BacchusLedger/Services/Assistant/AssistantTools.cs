using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BacchusLedger.Providers;

namespace BacchusLedger.Services.Assistant;

public sealed class AssistantTools(IFactsSource facts) : IAssistantTools
{
    static readonly AssistantToolDef[] All =
    [
        new("get_overview", "Cellar totals (wine in cellar now, wine lost, loss value in USD), above-normal flags and what 1% of loss costs per winery per year.", []),
        new("list_batches", "All real batches: code, winery, wine type, grapes kg, litres in/now/bottled, total loss and loss value.", []),
        new("get_batch", "One batch: every stage with litres before/after, loss and normal loss, the checks, grape origins (farmers, plots, kg), lab rows and totals.",
            [new("code", "string", "Batch code like W01-B1-2025, or just the winery code like W03", true)]),
        new("get_readings", "Fermentation readings of a batch or a vessel: temperature, ph, ta, initial_brix, hydrometer_density, hydrometer_abv.",
            [new("batch_or_vessel", "string", "Batch code (W03-B3-2025) or vessel code / cellar label (F-1, T3-2, Q-3)", true),
             new("type", "string", "Optional: temperature, ph, ta, initial_brix, hydrometer_density or hydrometer_abv"),
             new("from", "string", "Optional start date YYYY-MM-DD"), new("to", "string", "Optional end date YYYY-MM-DD")]),
        new("get_vessel", "One tank or qvevri: capacity, calibration, litres it holds now and which batches used it.",
            [new("cellar_label_or_code", "string", "Vessel code or cellar label, e.g. F-1, Q-3, T3-2", true)]),
        new("get_journal", "One of the 8 Kakheti PDO journals: 1 vineyard accounting, 2 grape receiving, 3 grape processing, 4 vintage, 5 storage and movement, 6 bottling, 7 laboratory analysis, 8 tasting commission.",
            [new("n", "integer", "Journal number 1-8", true), new("batch", "string", "Optional batch code")]),
        new("get_live_alerts", "Live notifications from LIVE mode (SIMULATED data) and whether Live is on.", []),
        new("search_facts", "Keyword search over the app's problem and market facts (farmers, companies, exports, value per litre, PDO zone, journals, loss norms, qvevri rule, climate, soil).",
            [new("query", "string", "English keywords — the facts are in English, so translate Georgian words first (e.g. farmers grapes)", true)]),
    ];

    public IReadOnlyList<AssistantToolDef> Defs => All;

    const int MaxJson = 12000;

    static string? A(JsonObject args, string k)
    {
        var n = args[k];
        if (n == null) return null;
        var s = (n.GetValueKind() == JsonValueKind.String ? n.GetValue<string>() : n.ToJsonString()).Trim();
        return s.Length == 0 ? null : s;
    }

    static JsonObject Pick(JsonNode? n, params string[] keys)
    {
        var o = new JsonObject();
        if (n is JsonObject src) foreach (var k in keys) if (src[k] is { } v) o[k] = v.DeepClone();
        return o;
    }
    static JsonArray PickAll(JsonNode? arr, Func<JsonNode, bool>? where, int max, params string[] keys) =>
        new((arr as JsonArray ?? []).OfType<JsonNode>().Where(x => where?.Invoke(x) != false).Take(max).Select(x => (JsonNode)Pick(x, keys)).ToArray());
    static bool NotCopy(JsonNode x) => x["copy"]?.GetValueKind() != JsonValueKind.True;
    /// Stage rows: the record's own source vs the calculated loss and the DEMO norm, so the model tags each number right.
    static JsonArray Tag(JsonArray stages)
    {
        foreach (var st in stages.OfType<JsonObject>())
        {
            if (st["source"] is { } src) { st.Remove("source"); st["litres_source"] = src; }
            st["loss_source"] = "Calculated";
            st["norm_source"] = "DEMO ASSUMPTION";
        }
        return stages;
    }
    static JsonObject Error(string e, JsonNode? extra = null) => new() { ["error"] = e, ["hint"] = extra };

    static async Task<(long id, string code)?> ResolveBatch(string? q, Func<string, Task<JsonNode?>> get)
    {
        if (q == null || await get("/api/batches") is not JsonArray all) return null;
        var list = all.OfType<JsonNode>().Select(b => (id: (long)b["id"]!, code: (string)b["code"]!, copy: !NotCopy(b))).ToList();
        var hit = list.FirstOrDefault(b => string.Equals(b.code, q, StringComparison.OrdinalIgnoreCase));
        if (hit.code == null) hit = list.FirstOrDefault(b => !b.copy && b.code.StartsWith(q, StringComparison.OrdinalIgnoreCase));
        return hit.code == null ? null : (hit.id, hit.code);
    }

    static async Task<JsonArray> BatchCodes(Func<string, Task<JsonNode?>> get) =>
        new((await get("/api/batches") as JsonArray ?? []).OfType<JsonNode>().Where(NotCopy).Select(b => (JsonNode)(string)b["code"]!).ToArray());

    static readonly Dictionary<string, string> TypeAlias = new(StringComparer.OrdinalIgnoreCase)
    {
        ["temp"] = "temperature", ["temperature"] = "temperature", ["ph"] = "ph", ["ta"] = "ta", ["acidity"] = "ta", ["titratable_acidity"] = "ta",
        ["brix"] = "initial_brix", ["initial_brix"] = "initial_brix", ["density"] = "hydrometer_density", ["sg"] = "hydrometer_density",
        ["hydrometer_density"] = "hydrometer_density", ["abv"] = "hydrometer_abv", ["alcohol"] = "hydrometer_abv", ["hydrometer_abv"] = "hydrometer_abv",
    };

    /// Runs one tool. Returns the JSON given to the model and a short "Used:" label for the chat.
    public async Task<(string Json, string Used)> RunAsync(string name, JsonObject args, Func<string, Task<JsonNode?>> get)
    {
        JsonObject res; string used;
        switch (name)
        {
            case "get_overview":
            {
                var o = await get("/api/overview");
                res = new JsonObject
                {
                    ["kpi"] = Pick(o?["kpi"], "wine_in_cellar_l", "batches", "total_loss_l", "loss_value_usd"),
                    ["flags"] = PickAll(o?["flags"], NotCopy, 20, "batch", "level", "stage", "date", "loss_l", "loss_pct", "norm_pct", "excess_l", "text", "detail", "source"),
                    ["one_percent_loss_per_year"] = PickAll(o?["one_percent"], null, 10, "winery", "size", "yearly_t", "litres_1pct", "usd", "up_to"),
                    ["value_per_litre_usd"] = o?["settings"]?["value_per_litre_usd"]?.DeepClone(),
                    ["sources"] = "kpi and flags: Calculated from the cellar operations (Dataset A6.1, A6.2); normal loss norms: DEMO ASSUMPTION; yearly_t: Challenge Book; one_percent values: Calculated; value_per_litre_usd: Calculated ($267.9M ÷ 89.7M L, Hackathon slides)",
                };
                used = "overview";
                break;
            }
            case "list_batches":
                res = new JsonObject
                {
                    ["batches"] = PickAll(await get("/api/batches"), NotCopy, 40, "code", "winery", "name", "wine_type", "technology", "variety", "vintage", "abv", "status",
                        "grapes_kg", "intake_date", "first_litres", "litres_now", "bottled_l", "bottles", "total_loss_l", "loss_value_usd", "above"),
                    ["sources"] = "grapes_kg, intake_date, wine_type: Dataset A2; litres and bottles: Dataset A6.1/A6.2 operations; total_loss_l, loss_value_usd, litres_now: Calculated; variety and abv: DEMO ASSUMPTION",
                };
                used = "batches";
                break;
            case "get_batch":
            {
                if (await ResolveBatch(A(args, "code"), get) is not { } b) { res = Error("batch not found", await BatchCodes(get)); used = $"batch {A(args, "code")}"; break; }
                var d = await get($"/api/batches/{b.id}");
                res = new JsonObject
                {
                    ["batch"] = Pick(d?["batch"], "code", "winery", "name", "wine_type", "technology", "variety", "vintage", "abv", "status", "grapes_kg", "intake_date", "source_note"),
                    ["stages"] = Tag(PickAll(d?["stages"], null, 30, "date", "stage_key", "from", "to", "before_l", "after_l", "remainder_l", "loss_l", "loss_pct", "norm_pct", "excess_l", "status",
                        "bottles", "bottle_ml", "method", "dip_cm", "source")),
                    ["totals"] = Pick(d?["totals"], "first_litres", "bottled_l", "bottles", "remainder_l", "total_loss_l", "total_difference_l", "litres_now", "loss_value_usd", "excess_l", "vessels"),
                    ["checks"] = PickAll(d?["checks"], null, 20, "label", "status", "detail", "source"),
                    ["origins"] = PickAll(d?["origins"], null, 20, "farmer", "plot", "municipality", "kg", "share_pct", "date", "sugar_gl", "source"),
                    ["lab"] = PickAll(d?["lab"], null, 20, "date", "vessel", "stage", "sugar_gl", "temp_c", "ph", "ta_gl", "rs_gl", "source"),
                    ["sources"] = "stages: litres_source tags the litres, loss_source tags loss_l/loss_pct/excess_l, norm_source tags norm_pct; totals: Calculated; origins and lab rows: their own source; farmers and plots: DEMO ASSUMPTION",
                };
                used = $"batch {b.code}";
                break;
            }
            case "get_readings":
            {
                var key = A(args, "batch_or_vessel");
                JsonNode? d; string label;
                if (await ResolveBatch(key, get) is { } b) { d = await get($"/api/readings?batch={b.id}"); label = b.code; }
                else if (key != null) { d = await get($"/api/readings?vessel={Uri.EscapeDataString(key)}"); label = key; }
                else { res = Error("batch_or_vessel is required"); used = "readings"; break; }
                if (d?["error"] != null) { res = Error($"no batch or vessel '{key}'", await BatchCodes(get)); used = $"readings {label}"; break; }
                var type = A(args, "type") is { } t ? TypeAlias.GetValueOrDefault(t.Replace(' ', '_'), t) : null;
                string? from = A(args, "from"), to = A(args, "to");
                var rows = (d?["rows"] as JsonArray ?? []).OfType<JsonNode>().Where(r =>
                {
                    var at = (string?)r["taken_at"] ?? "";
                    return (type == null || (string?)r["type"] == type) && (from == null || string.CompareOrdinal(at, from) >= 0) && (to == null || string.CompareOrdinal(at[..Math.Min(10, at.Length)], to) <= 0);
                }).ToList();
                res = new JsonObject
                {
                    ["count"] = rows.Count,
                    ["rows"] = PickAll(new JsonArray(rows.TakeLast(40).Select(r => r.DeepClone()).ToArray()), null, 40, "taken_at", "type", "value", "unit", "batch_code", "vessel_code", "cellar_label", "source", "time_in_dataset"),
                    ["initial_brix"] = PickAll(d?["initial_brix"], null, 10, "batch_code", "vessel_code", "taken_at", "initial_brix", "potential_abv", "source"),
                    ["sources"] = "each row has its own source; time_in_dataset=false means the dataset gives a date only (stored at 09:00); potential_abv: Calculated",
                };
                used = $"readings {label}" + (type != null ? $" ({type})" : "");
                break;
            }
            case "get_vessel":
            {
                var code = A(args, "cellar_label_or_code") ?? "";
                var d = await get($"/api/vessels/{Uri.EscapeDataString(code)}");
                if (d?["error"] != null || d?["vessel"] == null) { res = Error($"vessel '{code}' not found"); used = $"vessel {code}"; break; }
                res = new JsonObject
                {
                    ["vessel"] = Pick(d["vessel"], "winery", "code", "cellar_label", "type", "name", "capacity_l", "capacity_source", "calibrated", "calibration_note", "source",
                        "depth_cm", "neck_d_cm", "max_d_cm", "known_volume_l", "contents", "litres_now"),
                    ["batches"] = PickAll(d["batches"], null, 10, "code", "last"),
                    ["sources"] = "capacity: see capacity_source; litres_now and contents: Calculated from the operations",
                };
                used = $"vessel {(string?)d["vessel"]?["code"] ?? code}";
                break;
            }
            case "get_journal":
            {
                var nStr = A(args, "n");
                int n = double.TryParse(nStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var nd) ? (int)nd : 0;
                if (n is < 1 or > 8) { res = Error("journal number must be 1-8"); used = "journal"; break; }
                var bcode = A(args, "batch");
                var b = bcode == null ? null : await ResolveBatch(bcode, get);
                if (bcode != null && b == null) { res = Error("batch not found", await BatchCodes(get)); used = $"journal {n}"; break; }
                var j = await get($"/api/journals/{n}" + (b != null ? $"?batch={b.Value.id}" : ""));
                var rows = j?["rows"] as JsonArray ?? [];
                res = new JsonObject
                {
                    ["n"] = n, ["title"] = j?["title"]?.DeepClone(), ["note"] = j?["note"]?.DeepClone(),
                    ["columns"] = PickAll(j?["columns"], null, 40, "key", "label", "source"), ["row_count"] = rows.Count,
                    ["rows"] = new JsonArray(rows.Take(25).Select(r => r!.DeepClone()).ToArray()),
                    ["sources"] = "column sources are listed in columns[].source; Kakheti PDO spec §12",
                };
                used = $"journal {n}" + (b != null ? $" · {b.Value.code}" : "");
                break;
            }
            case "get_live_alerts":
            {
                var st = await get("/api/live/state");
                res = new JsonObject
                {
                    ["live_on"] = st?["on"]?.DeepClone(), ["scenario"] = st?["scenario_title"]?.DeepClone(),
                    ["notifications"] = PickAll(await get("/api/live/events?kind=notification&limit=10"), null, 10, "sim_time", "vessel_code", "vessel_name", "level", "rule", "message", "value_usd", "acknowledged", "source"),
                    ["sources"] = "SIMULATED (live simulator); SENSOR = sensor stub. Never used by journals, losses or ROI.",
                };
                used = "live alerts";
                break;
            }
            case "search_facts":
            {
                var q = A(args, "query") ?? "";
                res = new JsonObject { ["facts"] = new JsonArray(facts.Search(q).Select(f => (JsonNode)f).ToArray()), ["sources"] = "the source tag of each fact is in [brackets] at its end" };
                used = $"facts \"{q}\"";
                break;
            }
            default:
                res = Error($"unknown tool {name}");
                used = name;
                break;
        }
        var json = res.ToJsonString();
        return (json.Length > MaxJson ? json[..MaxJson] + " …(truncated)" : json, used);
    }
}
