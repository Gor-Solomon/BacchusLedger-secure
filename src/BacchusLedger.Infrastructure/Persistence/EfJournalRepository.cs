using BacchusLedger.Application.Repositories;
using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BacchusLedger.Infrastructure.Persistence;

/// Rows of journals 1, 2, 3, 4, 7 and 8. Each row is a column key → value map in the journal's column order, plus "source".
public sealed class EfJournalRepository(BacchusLedgerDbContext db) : IJournalRepository
{
    public Task<List<Dictionary<string, object?>>> RowsAsync(int journal, long? batch, string? winery) => journal switch
    {
        1 => VineyardAsync(batch, winery),
        2 => ReceivingAsync(batch, winery),
        3 => ProcessingAsync(batch, winery),
        4 => VintageAsync(batch, winery),
        7 => LaboratoryAsync(batch, winery),
        8 => TastingAsync(batch, winery),
        _ => throw new ArgumentOutOfRangeException(nameof(journal), "journals 5 and 6 come from the loss engine"),
    };

    async Task<List<Dictionary<string, object?>>> VineyardAsync(long? batch, string? winery)
    {
        var plots = db.Plots.AsNoTracking();
        if (batch != null) plots = plots.Where(p => db.BatchOrigins.Any(bo => bo.BatchId == batch && db.Deliveries.Any(d => d.Id == bo.DeliveryId && d.PlotId == p.Id)));
        if (!string.IsNullOrEmpty(winery)) plots = plots.Where(p => db.Wineries.Any(w => w.Id == p.WineryId && w.Code == winery));
        var rows = await (from p in plots
                          join f in db.Farmers on p.FarmerId equals f.Id
                          from wi in db.Wineries.Where(w => w.Id == p.WineryId).DefaultIfEmpty()
                          orderby p.Code
                          select new { Farmer = f.Name, p.Code, p.CadastreNo, p.Municipality, p.Region, p.AreaHa, p.Variety, p.LicenceNo, Winery = wi.Code, p.IsDemo })
            .ToListAsync();
        return rows.Select(r => Row(("farmer", r.Farmer), ("plot", r.Code), ("cadastre_no", r.CadastreNo), ("municipality", r.Municipality), ("region", r.Region),
            ("area_ha", r.AreaHa), ("variety", r.Variety), ("licence_no", r.LicenceNo), ("winery", r.Winery), ("source", DemoOrEntered(r.IsDemo)))).ToList();
    }

    async Task<List<Dictionary<string, object?>>> ReceivingAsync(long? batch, string? winery)
    {
        var batches = await BatchCodesByDeliveryAsync();
        var rows = await (from d in Deliveries(batch, winery)
                          join f in db.Farmers on d.FarmerId equals f.Id
                          join p in db.Plots on d.PlotId equals p.Id
                          orderby d.Date, d.Id
                          select new { d.Id, d.Date, Farmer = f.Name, Plot = p.Code, d.Kg, d.SugarGl, d.Accepted, d.Source }).ToListAsync();
        return rows.Select(r => Row(("date", r.Date), ("farmer", r.Farmer), ("plot", r.Plot), ("kg", r.Kg), ("sugar_gl", r.SugarGl),
            ("accepted", r.Accepted == 1 ? "yes" : "no"), ("batch", batches.GetValueOrDefault(r.Id)), ("source", r.Source))).ToList();
    }

    async Task<List<Dictionary<string, object?>>> ProcessingAsync(long? batch, string? winery)
    {
        var rows = await (from bo in db.BatchOrigins.AsNoTracking()
                          join b in Batches(batch, winery) on bo.BatchId equals b.Id
                          join d in db.Deliveries on bo.DeliveryId equals d.Id
                          join f in db.Farmers on d.FarmerId equals f.Id
                          join p in db.Plots on d.PlotId equals p.Id
                          orderby b.Code, d.Date, d.Id
                          select new
                          {
                              d.Date, Farmer = f.Name, Plot = p.Code, bo.Kg,
                              Share = bo.Kg == null || b.GrapesKg == null ? null : (double?)Math.Round(bo.Kg!.Value * 100.0 / b.GrapesKg!.Value, 2),
                              Batch = b.Code,
                              Vessel = db.Operations.Where(o => o.BatchId == b.Id && o.Type == "intake").OrderBy(o => o.Date)
                                  .Join(db.Vessels, o => o.ToVesselId, v => v.Id, (o, v) => new { v.Code, v.CellarLabel }).FirstOrDefault(),
                              IntakeDate = db.Operations.Where(o => o.BatchId == b.Id && o.Type == "intake").OrderBy(o => o.Date).Select(o => o.Date).FirstOrDefault(),
                              d.Source,
                          }).ToListAsync();
        return rows.Select(r => Row(("date", r.Date), ("farmer", r.Farmer), ("plot", r.Plot), ("kg", r.Kg), ("share_pct", r.Share), ("batch", r.Batch),
            ("vessel", r.Vessel == null ? null : VesselName(r.Vessel.Code, r.Vessel.CellarLabel)), ("intake_date", r.IntakeDate), ("source", r.Source))).ToList();
    }

    async Task<List<Dictionary<string, object?>>> VintageAsync(long? batch, string? winery)
    {
        var rows = await (from d in Deliveries(batch, winery)
                          join p in db.Plots on d.PlotId equals p.Id
                          join f in db.Farmers on p.FarmerId equals f.Id
                          group d by new { p.Id, p.Code, Farmer = f.Name, p.Variety, p.AreaHa, p.IsDemo } into g
                          orderby g.Key.Code
                          select new { g.Key.Code, g.Key.Farmer, g.Key.Variety, g.Key.AreaHa, g.Key.IsDemo, Kg = g.Sum(x => x.Kg), First = g.Min(x => x.Date) }).ToListAsync();
        // kg per ha as SQLite's ROUND(x, 0): half away from zero
        return rows.Select(r => Row(("plot", r.Code), ("farmer", r.Farmer), ("variety", r.Variety), ("area_ha", r.AreaHa), ("kg", r.Kg),
            ("kg_per_ha", r.Kg == null || r.AreaHa == null ? null : (double?)Math.Round(r.Kg.Value / r.AreaHa.Value, 0, MidpointRounding.AwayFromZero)),
            ("vintage", r.First?[..Math.Min(4, r.First.Length)]), ("source", DemoOrEntered(r.IsDemo)))).ToList();
    }

    async Task<List<Dictionary<string, object?>>> LaboratoryAsync(long? batch, string? winery)
    {
        var lab = await (from l in db.Lab.AsNoTracking()
                         join b in Batches(batch, winery) on l.BatchId equals b.Id
                         from v in db.Vessels.Where(v => v.Id == l.VesselId).DefaultIfEmpty()
                         orderby b.Code, l.Date, l.Id
                         select new { l.Date, Batch = b.Code, v.Code, v.CellarLabel, l.Stage, l.SugarGl, l.TempC, l.Ph, l.TaGl, l.RsGl, l.Note, l.Source }).ToListAsync();
        var rows = lab.Select(r => Row(("date", r.Date), ("batch", r.Batch), ("vessel", VesselName(r.Code, r.CellarLabel)), ("stage", r.Stage), ("sugar_gl", r.SugarGl),
            ("temp_c", r.TempC), ("ph", r.Ph), ("ta_gl", r.TaGl), ("rs_gl", r.RsGl), ("note", r.Note), ("source", r.Source))).ToList();
        // Readings typed into the app are lab analyses too; dataset readings are already lab rows above.
        var readings = await (from r in db.Readings.AsNoTracking()
                              join b in Batches(batch, winery) on r.BatchId equals b.Id
                              from v in db.Vessels.Where(v => v.Id == r.VesselId).DefaultIfEmpty()
                              where EF.Functions.Like(r.Source, "Entered%")
                              orderby b.Code, r.TakenAt, r.Id
                              // value as text the way SQLite writes a REAL (CAST(value AS TEXT), e.g. 22.0 → "22.0"), as the old SQL concatenation did
                              select new { r.TakenAt, Batch = b.Code, VesselId = (long?)v.Id, v.Code, v.CellarLabel, r.Type, r.Value, ValueText = r.Value.ToString(),
                                  r.Unit, r.Instrument, r.Person, r.Source }).ToListAsync();
        rows.AddRange(readings.Select(r => Row(("date", r.TakenAt?[..Math.Min(10, r.TakenAt.Length)]), ("batch", r.Batch),
            ("vessel", r.VesselId == null ? null : VesselName(r.Code, r.CellarLabel)), ("stage", "reading"),
            ("temp_c", r.Type == "temperature" ? r.Value : null), ("ph", r.Type == "ph" ? r.Value : null), ("ta_gl", r.Type == "ta" ? r.Value : null),
            ("note", r.Type == null || r.ValueText == null || r.Unit == null || r.TakenAt == null ? null
                : $"{r.Type} {r.ValueText} {r.Unit} at {Substr(r.TakenAt, 11, 5)}{(r.Instrument != null ? " · " + r.Instrument : "")}{(r.Person != null ? " · " + r.Person : "")}"),
            ("source", r.Source))));
        return rows;
    }

    async Task<List<Dictionary<string, object?>>> TastingAsync(long? batch, string? winery) =>
        (await Batches(batch, winery).OrderBy(b => b.Code).Select(b => new { b.Code, b.WineType, b.Variety, b.Vintage, b.Abv }).ToListAsync())
        .Select(b => Row(("batch", b.Code), ("wine_type", b.WineType), ("variety", b.Variety), ("vintage", b.Vintage), ("abv", b.Abv), ("tasting_date", ""),
            ("members", ""), ("appearance", ""), ("aroma", ""), ("taste", ""), ("score", ""), ("decision", ""), ("source", "Dataset A2"))).ToList();

    IQueryable<BatchEntity> Batches(long? batch, string? winery)
    {
        var q = db.Batches.AsNoTracking();
        if (batch != null) q = q.Where(b => b.Id == batch);
        if (!string.IsNullOrEmpty(winery)) q = q.Where(b => db.Wineries.Any(w => w.Id == b.WineryId && w.Code == winery));
        return q;
    }

    IQueryable<DeliveryEntity> Deliveries(long? batch, string? winery)
    {
        var q = db.Deliveries.AsNoTracking();
        if (batch != null) q = q.Where(d => db.BatchOrigins.Any(bo => bo.BatchId == batch && bo.DeliveryId == d.Id));
        if (!string.IsNullOrEmpty(winery)) q = q.Where(d => db.Plots.Any(p => p.Id == d.PlotId && db.Wineries.Any(w => w.Id == p.WineryId && w.Code == winery)));
        return q;
    }

    /// Batch codes per delivery, comma separated in batch_origins order (as group_concat gave them); missing = no batch.
    async Task<Dictionary<long, string>> BatchCodesByDeliveryAsync() =>
        (await (from bo in db.BatchOrigins.AsNoTracking()
                join b in db.Batches on bo.BatchId equals b.Id
                orderby bo.Rowid
                select new { bo.DeliveryId, b.Code }).ToListAsync())
        .GroupBy(x => x.DeliveryId ?? 0).ToDictionary(g => g.Key, g => string.Join(",", g.Select(x => x.Code)));

    /// Cellar label with the code, or the code alone (SQL: CASE WHEN COALESCE(cellar_label,'')='' THEN code ELSE label || ' (' || code || ')' END).
    static string? VesselName(string? code, string? label) => string.IsNullOrEmpty(label) ? code : code == null ? null : $"{label} ({code})";

    static string DemoOrEntered(long? isDemo) => isDemo == 1 ? "DEMO ASSUMPTION" : "Entered";

    /// SQLite substr(s, start + 1, length).
    static string Substr(string s, int start, int length) => start >= s.Length ? "" : s.Substring(start, Math.Min(length, s.Length - start));

    static Dictionary<string, object?> Row(params (string Key, object? Value)[] cells) => cells.ToDictionary(c => c.Key, c => c.Value);
}
