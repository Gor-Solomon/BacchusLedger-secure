using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace BacchusLedger;

/// Tamper evidence (secure copy only). Every record entry of a batch (operations, lab, readings) is chained:
/// hash = SHA-256(previous hash | table | row content). The final hash of each batch is signed with a DEMO ECDSA P-256 key
/// kept in a local file outside the code. Verify recomputes the chain from the rows as they are now.
public static class Seal
{
    public const string Tables = """
        CREATE TABLE IF NOT EXISTS seal_chain(batch_id INTEGER, seq INTEGER, tbl TEXT, row_id INTEGER, prev_hash TEXT, hash TEXT, PRIMARY KEY(batch_id, seq));
        CREATE TABLE IF NOT EXISTS seal_meta(key TEXT PRIMARY KEY, value TEXT);
        CREATE TABLE IF NOT EXISTS seal_tamper(tbl TEXT, row_id INTEGER, col TEXT, old_value REAL, batch_id INTEGER);
        CREATE TABLE IF NOT EXISTS seal_batch(batch_id INTEGER PRIMARY KEY, batch_code TEXT, final_hash TEXT, signature TEXT, sealed_at TEXT, entries INTEGER, key_id TEXT);
        """;
    // Chain order: vineyard deliveries first, then the cellar record. Version 2 added deliveries (older seals are rebuilt once).
    static readonly string[] EntryTables = ["deliveries", "operations", "lab", "readings"];
    const string Version = "2";

    // ---------------------------------------------------------------- DEMO signing key (local file, never in the code or the image)
    public static readonly string KeyPath = Environment.GetEnvironmentVariable("SIGNING_KEY_PATH")
        ?? Path.Combine(AppContext.BaseDirectory, "keys", "DEMO_KEY_ecdsa_p256_NOT_FOR_PRODUCTION.pem");
    static readonly Lazy<ECDsa> Key = new(() =>
    {
        var k = ECDsa.Create();
        if (File.Exists(KeyPath)) { k.ImportFromPem(File.ReadAllText(KeyPath)); return k; }
        k = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Directory.CreateDirectory(Path.GetDirectoryName(KeyPath)!);
        File.WriteAllText(KeyPath, "DEMO KEY - Bacchus Ledger secure copy - NOT FOR PRODUCTION. Generated " + DateTime.UtcNow.ToString("o") + "\n" + k.ExportPkcs8PrivateKeyPem() + "\n");
        Console.WriteLine($"seal: generated DEMO signing key at {KeyPath}");
        return k;
    });
    public static string PublicKeyPem => Key.Value.ExportSubjectPublicKeyInfoPem();
    public static string KeyId => Convert.ToHexString(SHA256.HashData(Key.Value.ExportSubjectPublicKeyInfo()))[..16].ToLowerInvariant();
    public static string Sign(string finalHash) => Convert.ToBase64String(Key.Value.SignData(Encoding.UTF8.GetBytes(finalHash), HashAlgorithmName.SHA256));
    public static bool CheckSignature(string finalHash, string signature)
    {
        try { return Key.Value.VerifyData(Encoding.UTF8.GetBytes(finalHash), Convert.FromBase64String(signature), HashAlgorithmName.SHA256); }
        catch (FormatException) { return false; }
    }

    // ---------------------------------------------------------------- chain
    static string Sha(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
    static string Genesis(string batchCode) => Sha("genesis|" + batchCode);
    public static string EntryHash(string prev, string tbl, Dictionary<string, object?> row) => Sha($"{prev}|{tbl}|{Content(row)}");
    /// Canonical row content: all columns, sorted by name, as JSON.
    static string Content(Dictionary<string, object?> row) => JsonSerializer.Serialize(new SortedDictionary<string, object?>(row, StringComparer.Ordinal));

    static Dictionary<string, object?>? Row(SqliteConnection c, string tbl, long id) =>
        Db.Query(c, $"SELECT * FROM {tbl} WHERE id=$1", id).FirstOrDefault();

    static List<(string tbl, long id)> CurrentEntries(SqliteConnection c, long batchId) =>
        EntryTables.SelectMany(t => Db.Query(c, t == "deliveries"
            ? "SELECT DISTINCT d.id FROM deliveries d JOIN batch_origins bo ON bo.delivery_id=d.id WHERE bo.batch_id=$1 ORDER BY d.id"
            : $"SELECT id FROM {t} WHERE batch_id=$1 ORDER BY id", batchId).Select(r => (t, (long)r["id"]!))).ToList();

    record Stored(long Seq, string Tbl, long RowId, string Prev, string Hash);
    static List<Stored> Chain(SqliteConnection c, long batchId) =>
        Db.Query(c, "SELECT * FROM seal_chain WHERE batch_id=$1 ORDER BY seq", batchId)
          .Select(r => new Stored((long)r["seq"]!, (string)r["tbl"]!, (long)r["row_id"]!, (string)r["prev_hash"]!, (string)r["hash"]!)).ToList();

    /// Adds new entries to the end of the chain and re-signs the final hash. Entries already in the chain are never rehashed,
    /// so a changed row stays detectable. A batch whose sealed rows are all gone (demo reset) starts a fresh chain.
    public static void Ensure(SqliteConnection c, SqliteTransaction tx, long batchId)
    {
        var code = (string)Db.Query(c, "SELECT code FROM batches WHERE id=$1", batchId)[0]["code"]!;
        var chain = Chain(c, batchId);
        var seal = Db.Query(c, "SELECT * FROM seal_batch WHERE batch_id=$1", batchId).FirstOrDefault();
        if (chain.Count > 0 && (seal?["batch_code"] as string != code || chain.All(e => Row(c, e.Tbl, e.RowId) == null)))
        {
            Db.Exec(c, tx, "DELETE FROM seal_chain WHERE batch_id=$1", batchId);
            Db.Exec(c, tx, "DELETE FROM seal_batch WHERE batch_id=$1", batchId);
            chain.Clear(); seal = null;
        }
        var have = chain.Select(e => (e.Tbl, e.RowId)).ToHashSet();
        var prev = chain.Count > 0 ? chain[^1].Hash : Genesis(code);
        long seq = chain.Count;
        int added = 0;
        foreach (var (t, id) in CurrentEntries(c, batchId).Where(e => !have.Contains(e)))
        {
            var h = EntryHash(prev, t, Row(c, t, id)!);
            Db.Exec(c, tx, "INSERT INTO seal_chain(batch_id,seq,tbl,row_id,prev_hash,hash) VALUES($1,$2,$3,$4,$5,$6)", batchId, ++seq, t, id, prev, h);
            prev = h; added++;
        }
        if (added == 0 && seal != null) return;
        Db.Exec(c, tx, """
            INSERT OR REPLACE INTO seal_batch(batch_id,batch_code,final_hash,signature,sealed_at,entries,key_id) VALUES($1,$2,$3,$4,$5,$6,$7)
            """, batchId, code, prev, Sign(prev), DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), seq, KeyId);
    }

    public static void Drop(SqliteConnection c, SqliteTransaction tx, long batchId)
    {
        Db.Exec(c, tx, "DELETE FROM seal_chain WHERE batch_id=$1", batchId);
        Db.Exec(c, tx, "DELETE FROM seal_batch WHERE batch_id=$1", batchId);
        Db.Exec(c, tx, "DELETE FROM seal_tamper WHERE batch_id=$1", batchId);
    }

    /// Hash of every sealed entry (for the Hash column of the report).
    public static List<object> Entries(SqliteConnection c, long batchId) =>
        Chain(c, batchId).Select(e => (object)new { seq = e.Seq, tbl = e.Tbl, row_id = e.RowId, hash = e.Hash, prev = e.Prev }).ToList();

    /// DEMO ONLY: put back the values changed by TamperDemo; if the chain still does not verify, seal the batch again from its current rows.
    public static object ResetDemo(SqliteConnection c, SqliteTransaction tx, long batchId)
    {
        var t = Db.Query(c, "SELECT * FROM seal_tamper WHERE batch_id=$1 ORDER BY rowid DESC", batchId);
        foreach (var r in t) Db.Exec(c, tx, "UPDATE lab SET temp_c=$1 WHERE id=$2", r["old_value"], r["row_id"]);
        Db.Exec(c, tx, "DELETE FROM seal_tamper WHERE batch_id=$1", batchId);
        bool resealed = false;
        if (!Verify(c, batchId).Ok) { Drop(c, tx, batchId); Ensure(c, tx, batchId); resealed = true; }
        return new { restored = t.Count, resealed };
    }

    public static void EnsureAll() => Db.Write((c, tx) =>
    {
        Db.Exec(c, tx, Tables);
        if (Db.Query(c, "SELECT value FROM seal_meta WHERE key='version'").FirstOrDefault()?["value"] as string != Version)
        {
            Console.WriteLine($"seal: chain format v{Version} (deliveries first) — sealing all batches again");
            foreach (var t in new[] { "seal_chain", "seal_batch", "seal_tamper" }) Db.Exec(c, tx, $"DELETE FROM {t}");
            Db.Exec(c, tx, "INSERT OR REPLACE INTO seal_meta(key,value) VALUES('version',$1)", Version);
        }
        foreach (var b in Db.Query(c, "SELECT id FROM batches ORDER BY id")) Ensure(c, tx, (long)b["id"]!);
        return 0;
    });

    public sealed record Result(bool Ok, string Status, long? TamperedAt, string? TamperedWhat, string? FinalHash, string? Signature, string? SealedAt,
        long Entries, int Unsealed, bool SignatureOk, string KeyId);

    /// Recomputes the chain from the rows as they are now and compares with the sealed hashes and the signature.
    public static Result Verify(SqliteConnection c, long batchId)
    {
        var seal = Db.Query(c, "SELECT * FROM seal_batch WHERE batch_id=$1", batchId).FirstOrDefault();
        if (seal == null) return new(false, "not sealed", null, null, null, null, null, 0, 0, false, KeyId);
        string code = (string)seal["batch_code"]!, final = (string)seal["final_hash"]!, sig = (string)seal["signature"]!;
        var chain = Chain(c, batchId);
        var prev = Genesis(code);
        foreach (var e in chain)
        {
            var row = Row(c, e.Tbl, e.RowId);
            var what = $"{e.Tbl} #{e.RowId}" + ((row?.GetValueOrDefault("date") ?? row?.GetValueOrDefault("taken_at")) is string d ? $" ({d})" : "");
            if (row == null) return Bad(e.Seq, what + " — deleted");
            var h = EntryHash(prev, e.Tbl, row);
            if (h != e.Hash || e.Prev != prev) return Bad(e.Seq, what + " — content changed");
            prev = h;
        }
        bool sigOk = CheckSignature(final, sig);
        if (prev != final || !sigOk) return Bad(chain.Count, sigOk ? "final hash does not match the seal" : "signature invalid", sigOk);
        var have = chain.Select(e => (e.Tbl, e.RowId)).ToHashSet();
        int unsealed = CurrentEntries(c, batchId).Count(e => !have.Contains(e));
        return new(true, "verified", null, null, final, sig, seal["sealed_at"] as string, chain.Count, unsealed, true, KeyId);

        Result Bad(long seq, string what, bool? s = null) =>
            new(false, "tampered", seq, what, final, sig, seal["sealed_at"] as string, chain.Count, 0, s ?? CheckSignature(final, sig), KeyId);
    }

    /// DEMO ONLY (secure copy): change one value of a sealed entry so Verify shows the ❌ result.
    /// Changes a lab temperature (no trigger on lab), so ledger balances stay as they are.
    public sealed record Tampered(long Entry, string Table, long RowId, string? Date, string Field, double Before, double After);
    public static Tampered? TamperDemo(SqliteConnection c, SqliteTransaction tx, long batchId)
    {
        var e = Db.Query(c, """
            SELECT s.seq, l.id, l.date, l.temp_c FROM seal_chain s JOIN lab l ON l.id=s.row_id
            WHERE s.batch_id=$1 AND s.tbl='lab' AND l.temp_c IS NOT NULL ORDER BY s.seq LIMIT 1
            """, batchId).FirstOrDefault();
        if (e == null) return null;
        var before = Db.D(e["temp_c"])!.Value;
        Db.Exec(c, tx, "UPDATE lab SET temp_c=$1 WHERE id=$2", Math.Round(before + 1.5, 1), e["id"]);
        if (Db.Query(c, "SELECT 1 FROM seal_tamper WHERE tbl='lab' AND row_id=$1", e["id"]).Count == 0)
            Db.Exec(c, tx, "INSERT INTO seal_tamper(tbl,row_id,col,old_value,batch_id) VALUES('lab',$1,'temp_c',$2,$3)", e["id"], before, batchId);
        return new((long)e["seq"]!, "lab", (long)e["id"]!, e["date"] as string, "temp_c", before, Math.Round(before + 1.5, 1));
    }
}

/// Selftest: chain + signature (rolled back).
public static class SealSelfTest
{
    public static void Run(Action<string, string, string, string, bool> Add)
    {
        const string g = "seal";
        try
        {
            var (ok, tampered, at, sigBad) = Db.Probe((c, tx) =>
            {
                Db.Exec(c, tx, Seal.Tables);
                var id = (long)Db.Query(c, "SELECT id FROM batches WHERE code='W03-B3-2025'")[0]["id"]!;
                Seal.Ensure(c, tx, id);
                var v1 = Seal.Verify(c, id);
                var t = Seal.TamperDemo(c, tx, id);
                var v2 = Seal.Verify(c, id);
                return (v1, v2, t, Seal.CheckSignature(v1.FinalHash + "0", v1.Signature!));
            });
            Add(g, "W03-B3-2025: chain + signature verify unchanged", "verified", $"{ok.Status}, {ok.Entries} entries, signature {(ok.SignatureOk ? "ok" : "bad")}", ok.Ok && ok.SignatureOk && ok.Entries > 0);
            Add(g, "Changing one lab value is detected at that entry", $"tampered at {at?.Entry}", $"{tampered.Status} at {tampered.TamperedAt}",
                !tampered.Ok && tampered.Status == "tampered" && at != null && tampered.TamperedAt == at.Entry);
            Add(g, "Signature does not verify for a different hash", "invalid", sigBad ? "valid" : "invalid", !sigBad);
        }
        catch (Exception e) { Add(g, "seal", "ok", e.Message, false); }
    }
}
