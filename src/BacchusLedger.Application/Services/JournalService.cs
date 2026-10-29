using System.Globalization;
using System.Text;
using BacchusLedger.Application.Contracts.Journals;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Domain.Common;

namespace BacchusLedger.Application.Services;

public sealed class JournalService(IJournalRepository journals, IBatchRepository batches, ISettingsRepository settings, ILossAnalysisService losses) : IJournalService
{
    static readonly string[] Titles =
    [
        "", "Vineyard accounting", "Grape receiving", "Grape processing", "Vintage",
        "Storage and movement", "Bottling", "Laboratory analysis", "Tasting commission",
    ];

    public async Task<IReadOnlyList<JournalCoverageDto>> CoverageAsync(long? batch, string? winery)
    {
        var list = new List<JournalCoverageDto>();
        for (int n = 1; n <= 8; n++)
        {
            var j = await BuildAsync(n, batch, winery);
            list.Add(new JournalCoverageDto(n, j.Title, j.Rows.Count, j.Rows.Count > 0, n == 8));
        }
        return list;
    }

    public async Task<ServiceResult<JournalDto>> GetAsync(int n, long? batch, string? winery) =>
        n is < 1 or > 8 ? ServiceResult<JournalDto>.NotFound() : await BuildAsync(n, batch, winery);

    public async Task<ServiceResult<JournalCsvFile>> CsvAsync(int n, long? batch, string? winery)
    {
        if (n is < 1 or > 8) return ServiceResult<JournalCsvFile>.NotFound();
        var j = await BuildAsync(n, batch, winery);
        return new JournalCsvFile(Csv(j), $"journal-{n}-{j.Title.ToLowerInvariant().Replace(' ', '-')}.csv");
    }

    public async Task<JournalDto> BuildAsync(int n, long? batch, string? winery)
    {
        static JournalColumnDto C(string key, string label, string? source = null) => new(key, label, source);
        var (columns, note) = n switch
        {
            1 => (new[] { C("farmer", "Farmer"), C("plot", "Plot"), C("cadastre_no", "Cadastre no."), C("municipality", "Municipality"), C("region", "Region"),
                    C("area_ha", "Area ha"), C("variety", "Variety"), C("licence_no", "Licence no."), C("winery", "Winery") },
                "Farmers and plots are DEMO ASSUMPTIONS — the dataset has no farmers."),
            2 => (new[] { C("date", "Date"), C("farmer", "Farmer"), C("plot", "Plot"), C("kg", "kg"), C("sugar_gl", "Sugar g/L"), C("accepted", "Accepted"), C("batch", "Batch") },
                "kg per delivery is a DEMO split of the Dataset A2 batch total. Sugar 233 g/L = Dataset A3 day 1 (must in F-1)."),
            3 => (new[] { C("date", "Delivery date"), C("farmer", "Farmer"), C("plot", "Plot"), C("kg", "kg"), C("share_pct", "Share %", "Calculated"),
                    C("batch", "Batch"), C("vessel", "Into vessel"), C("intake_date", "Intake date") },
                "Links every delivery to its batch and intake vessel. Share % is Calculated."),
            4 => (new[] { C("plot", "Plot"), C("farmer", "Farmer"), C("variety", "Variety"), C("area_ha", "Area ha"), C("kg", "Harvest kg"),
                    C("kg_per_ha", "kg per ha", "Calculated"), C("vintage", "Vintage") },
                "kg per ha = harvest kg ÷ plot area (Calculated)."),
            5 => (new[] { C("date", "Date"), C("batch", "Batch"), C("op", "Operation"), C("from", "From"), C("to", "To"), C("before_l", "Before L", "Calculated"),
                    C("litres", "Litres"), C("remainder_l", "Remainder L"), C("loss_l", "Loss L", "Calculated"), C("loss_pct", "Loss %", "Calculated"),
                    C("status", "Status"), C("method", "Method"), C("note", "Note") },
                "Every cellar movement with from/to, litres and loss (mass balance, Calculated). Bottling runs are in journal 6."),
            6 => (new[] { C("date", "Date"), C("batch", "Batch"), C("op", "Operation"), C("vessel", "From vessel"), C("bottles", "Bottles"), C("bottle_ml", "Bottle mL"),
                    C("litres", "Litres bottled", "Calculated"), C("remainder_l", "Remainder L"), C("loss_l", "Loss L", "Calculated"), C("check", "Bottle check") },
                "Bottling runs and transfer to finished goods."),
            7 => (new[] { C("date", "Date"), C("batch", "Batch"), C("vessel", "Vessel"), C("stage", "Stage"), C("sugar_gl", "Sugar g/L"), C("temp_c", "Temp °C"),
                    C("ph", "pH"), C("ta_gl", "TA g/L"), C("rs_gl", "RS g/L"), C("note", "Note") },
                "All laboratory rows (Dataset A3, A4.1, A4.2) plus readings entered in the app (Initial Brix, hydrometer, pH, temperature, TA)."),
            8 => (new[] { C("batch", "Batch"), C("wine_type", "Wine type"), C("variety", "Variety"), C("vintage", "Vintage"), C("abv", "ABV %"),
                    C("tasting_date", "Tasting date"), C("members", "Commission members"), C("appearance", "Appearance"), C("aroma", "Aroma"),
                    C("taste", "Taste"), C("score", "Score"), C("decision", "Decision") },
                "Empty template — there is no tasting data in the dataset. Variety and ABV are DEMO ASSUMPTIONS."),
            _ => throw new ArgumentOutOfRangeException(nameof(n)),
        };
        var rows = n is 5 or 6 ? await LossRowsAsync(n, batch, winery) : await journals.RowsAsync(n, batch, winery);
        return new JournalDto(n, Titles[n], note, columns, rows.Select(r => (IReadOnlyDictionary<string, object?>)r).ToList());
    }

    /// Journal 5 (movements) and 6 (bottling) come from the replayed stages of each batch.
    async Task<List<Dictionary<string, object?>>> LossRowsAsync(int n, long? batch, string? winery)
    {
        var s = await settings.GetAsync();
        var rows = new List<Dictionary<string, object?>>();
        var ids = await batches.ListIdsAsync(batch, winery);
        var results = await losses.AnalyzeManyAsync(ids, s);
        foreach (var id in ids)
        {
            var r = results[id];
            if (n == 5)
                foreach (var st in r.Stages.Where(x => x.Type is not ("bottling" or "warehouse")))
                    rows.Add(new()
                    {
                        ["date"] = st.Date, ["batch"] = r.BatchCode, ["op"] = st.StageKey, ["from"] = st.From.WithCellarLabel(st.FromLabel), ["to"] = st.To.WithCellarLabel(st.ToLabel), ["before_l"] = st.BeforeL,
                        ["litres"] = st.AfterL, ["remainder_l"] = st.RemainderL, ["loss_l"] = st.LossL, ["loss_pct"] = st.LossPct, ["status"] = st.Status,
                        ["method"] = st.Method, ["note"] = st.Note, ["source"] = st.Source,
                    });
            else
                foreach (var st in r.Stages.Where(x => x.Type is "bottling" or "warehouse"))
                {
                    bool isB = st.Type == "bottling";
                    double? lit = isB ? st.AfterL : null;
                    rows.Add(new()
                    {
                        ["date"] = st.Date, ["batch"] = r.BatchCode, ["op"] = st.Type, ["vessel"] = st.From.WithCellarLabel(st.FromLabel), ["bottles"] = st.Bottles, ["bottle_ml"] = st.BottleMl,
                        ["litres"] = lit, ["remainder_l"] = st.RemainderL, ["loss_l"] = st.LossL,
                        ["check"] = isB ? $"{st.Bottles:N0} × {(st.BottleMl ?? 750) / 1000:0.00#} = {lit:N0} L ✓" : (st.Note ?? ""), ["source"] = st.Source,
                    });
                }
        }
        return rows;
    }

    /// UTF-8 with BOM so Excel opens Georgian text.
    static byte[] Csv(JournalDto j)
    {
        var sb = new StringBuilder();
        static string Esc(object? v)
        {
            var s = v switch { null => "", double d => d.ToString(CultureInfo.InvariantCulture), IFormattable f => f.ToString(null, CultureInfo.InvariantCulture), _ => v.ToString() ?? "" };
            return s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
        sb.AppendLine($"# Journal {j.N}: {Esc(j.Title)} (Kakheti PDO spec §12)");
        sb.AppendLine(string.Join(",", j.Columns.Select(c => Esc(c.Label)).Append("Source")));
        foreach (var r in j.Rows)
            sb.AppendLine(string.Join(",", j.Columns.Select(c => Esc(r.GetValueOrDefault(c.Key))).Append(Esc(r.GetValueOrDefault("source")))));
        sb.AppendLine($"# {Esc(j.Note)}");
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }
}
