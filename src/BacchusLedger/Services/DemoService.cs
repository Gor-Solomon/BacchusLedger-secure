using BacchusLedger.Contracts.Demo;

namespace BacchusLedger.Services;

public sealed class DemoService : IDemoService
{
    public DemoStepsDto Steps(string? script) => DemoScript.Steps(Script(script));

    public ServiceResult<DemoCardDto> Step(int n, string? script)
    {
        try { return DemoScript.Step(Script(script), n); }
        catch (ArgumentOutOfRangeException) { return ServiceResult<DemoCardDto>.NotFound("no such step"); }
    }

    public DemoStepsDto Reset(string? script)
    {
        DemoScript.Reset(Script(script));
        return DemoScript.Steps(Script(script));
    }

    public Task<PracticeCopyResponse> CreatePracticeCopyAsync() => Task.FromResult(Db.Write((c, tx) =>
    {
        var src = Seed.Batches.First(x => x.Code == "W03-B3-2025");
        int k = (int)(Db.Query(c, "SELECT COUNT(*) n FROM batches WHERE code LIKE 'W03-B3-2025-TRY-%'")[0]["n"] as long? ?? 0) + 1;
        var code = $"W03-B3-2025-TRY-{k}";
        var id = Seed.InsertBatch(c, tx, src, code, "in progress (practice copy)", "Dataset A6.1 — practice copy of W03-B3-2025");
        foreach (var o in Db.Query(c, "SELECT bo.delivery_id, bo.kg FROM batch_origins bo JOIN batches b ON b.id=bo.batch_id WHERE b.code='W03-B3-2025'"))
            Db.Exec(c, tx, "INSERT INTO batch_origins(batch_id,delivery_id,kg) VALUES($1,$2,$3)", id, o["delivery_id"], o["kg"]);
        foreach (var o in Seed.W03Ops.Take(4)) Seed.InsertOp(c, tx, id, "W03", o, "Dataset A6.1");
        return new PracticeCopyResponse(id, code);
    }));

    public Task<LiveDemoStartResponse> StartLiveReadingsAsync(string? vessel) => Task.FromResult(Db.Write((c, tx) =>
    {
        const string code = "W01-LIVE-DEMO";
        var old = Db.Query(c, "SELECT id FROM batches WHERE code=$1", code).FirstOrDefault()?["id"];
        Db.AllowDelete(c, tx, () =>
        {
            if (old != null) Db.Exec(c, tx, "DELETE FROM readings WHERE batch_id=$1", old);
        });
        if (old != null) Db.Exec(c, tx, "DELETE FROM batches WHERE id=$1", old);
        Db.Exec(c, tx, """
            DELETE FROM vessels WHERE source='Entered (live demo)'
              AND id NOT IN (SELECT from_vessel_id FROM operations WHERE from_vessel_id IS NOT NULL UNION SELECT to_vessel_id FROM operations WHERE to_vessel_id IS NOT NULL
                             UNION SELECT vessel_id FROM readings WHERE vessel_id IS NOT NULL UNION SELECT vessel_id FROM lab WHERE vessel_id IS NOT NULL)
            """);
        Db.Exec(c, tx, "UPDATE vessels SET cellar_label=NULL, cellar_label_source=NULL WHERE cellar_label_source='Entered (live demo)'");
        var vcode = "F-2";
        if (vessel == "new")
        {
            vcode = "Q-LIVE";
            if (Db.Query(c, "SELECT 1 FROM vessels v JOIN wineries w ON w.id=v.winery_id WHERE w.code='W01' AND v.code=$1", vcode).Count == 0)
                Db.Exec(c, tx, "INSERT INTO vessels(winery_id,code,type,name,calibration_json,capacity_l,source) VALUES((SELECT id FROM wineries WHERE code='W01'),$1,'qvevri','New qvevri (live demo)',NULL,NULL,'Entered (live demo)')", vcode);
        }
        var id = Db.Insert(c, tx, """
            INSERT INTO batches(winery_id,code,name,wine_type,technology,variety,vintage,abv,status,source_note,grapes_kg)
            VALUES((SELECT id FROM wineries WHERE code='W01'),$1,'Live demo batch','Live demo (values entered by the presenter)',$2,NULL,NULL,NULL,'fermenting (live demo)',
              'Entered (live demo) — readings typed during the presentation; live demo copy',NULL)
            """, code, vessel == "new" ? "qvevri" : "classic");
        return new LiveDemoStartResponse(id, code, vcode, "W01");
    }));

    static string Script(string? s) => s == "w03" ? "w03" : "w01";
}
