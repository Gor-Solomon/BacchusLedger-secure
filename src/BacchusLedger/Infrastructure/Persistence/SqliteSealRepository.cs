using BacchusLedger.Models;
using BacchusLedger.Repositories;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class SqliteSealRepository(ISqliteConnectionFactory connections) : ISealRepository
{
    /// Chain order: vineyard deliveries first, then the cellar record.
    static readonly string[] EntryTables = ["deliveries", "operations", "lab", "readings"];

    public Task EnsureTablesAsync() => Exec("""
        CREATE TABLE IF NOT EXISTS seal_chain(batch_id INTEGER, seq INTEGER, tbl TEXT, row_id INTEGER, prev_hash TEXT, hash TEXT, PRIMARY KEY(batch_id, seq));
        CREATE TABLE IF NOT EXISTS seal_meta(key TEXT PRIMARY KEY, value TEXT);
        CREATE TABLE IF NOT EXISTS seal_tamper(tbl TEXT, row_id INTEGER, col TEXT, old_value REAL, batch_id INTEGER);
        CREATE TABLE IF NOT EXISTS seal_batch(batch_id INTEGER PRIMARY KEY, batch_code TEXT, final_hash TEXT, signature TEXT, sealed_at TEXT, entries INTEGER, key_id TEXT);
        """);

    public async Task<string?> GetVersionAsync()
    {
        await using var db = await connections.OpenAsync();
        return (await db.QueryAsync("SELECT value FROM seal_meta WHERE key='version'", r => r.Str("value"))).FirstOrDefault();
    }

    public Task SetVersionAsync(string version) => Exec("INSERT OR REPLACE INTO seal_meta(key,value) VALUES('version',$1)", version);

    public async Task ClearAllAsync()
    {
        foreach (var t in new[] { "seal_chain", "seal_batch", "seal_tamper" }) await Exec($"DELETE FROM {t}");
    }

    public async Task<List<SealChainEntry>> ChainAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("SELECT * FROM seal_chain WHERE batch_id=$1 ORDER BY seq",
            r => new SealChainEntry(r.Long("seq"), r.Str("tbl")!, r.Long("row_id"), r.Str("prev_hash")!, r.Str("hash")!), batchId);
    }

    public async Task<SealRecord?> GetAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        return await db.FirstOrDefaultAsync("SELECT * FROM seal_batch WHERE batch_id=$1", r => new SealRecord(r.Long("batch_id"), r.Str("batch_code")!, r.Str("final_hash")!,
            r.Str("signature")!, r.Str("sealed_at"), r.LongOrNull("entries"), r.Str("key_id")), batchId);
    }

    public async Task<List<SealEntryRef>> CurrentEntriesAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        var list = new List<SealEntryRef>();
        foreach (var t in EntryTables)
            list.AddRange(await db.QueryAsync(t == "deliveries"
                ? "SELECT DISTINCT d.id FROM deliveries d JOIN batch_origins bo ON bo.delivery_id=d.id WHERE bo.batch_id=$1 ORDER BY d.id"
                : $"SELECT id FROM {t} WHERE batch_id=$1 ORDER BY id", r => new SealEntryRef(t, r.Long("id")), batchId));
        return list;
    }

    public async Task<Dictionary<string, object?>?> GetRowAsync(string table, long id)
    {
        if (!EntryTables.Contains(table)) throw new ArgumentException($"not a sealed table: {table}");
        await using var db = await connections.OpenAsync();
        return (await db.QueryRowsAsync($"SELECT * FROM {table} WHERE id=$1", id)).FirstOrDefault();
    }

    public Task AddEntryAsync(long batchId, long seq, string table, long rowId, string prev, string hash) =>
        Exec("INSERT INTO seal_chain(batch_id,seq,tbl,row_id,prev_hash,hash) VALUES($1,$2,$3,$4,$5,$6)", batchId, seq, table, rowId, prev, hash);

    public Task SaveAsync(SealRecord s) => Exec("""
        INSERT OR REPLACE INTO seal_batch(batch_id,batch_code,final_hash,signature,sealed_at,entries,key_id) VALUES($1,$2,$3,$4,$5,$6,$7)
        """, s.BatchId, s.BatchCode, s.FinalHash, s.Signature, s.SealedAt, s.Entries, s.KeyId);

    public async Task DeleteChainAndSealAsync(long batchId)
    {
        await Exec("DELETE FROM seal_chain WHERE batch_id=$1", batchId);
        await Exec("DELETE FROM seal_batch WHERE batch_id=$1", batchId);
    }

    public Task DeleteTamperRecordsAsync(long batchId) => Exec("DELETE FROM seal_tamper WHERE batch_id=$1", batchId);

    public async Task<List<TamperRecord>> TamperRecordsAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("SELECT * FROM seal_tamper WHERE batch_id=$1 ORDER BY rowid DESC", r => new TamperRecord(r.Long("row_id"), r.Dbl("old_value")), batchId);
    }

    public async Task<SealedLabTemperature?> FirstSealedLabTemperatureAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        return await db.FirstOrDefaultAsync("""
            SELECT s.seq, l.id, l.date, l.temp_c FROM seal_chain s JOIN lab l ON l.id=s.row_id
            WHERE s.batch_id=$1 AND s.tbl='lab' AND l.temp_c IS NOT NULL ORDER BY s.seq LIMIT 1
            """, r => new SealedLabTemperature(r.Long("seq"), r.Long("id"), r.Str("date"), r.Dbl("temp_c")!.Value), batchId);
    }

    public Task SetLabTemperatureAsync(long labId, double? value) => Exec("UPDATE lab SET temp_c=$1 WHERE id=$2", value, labId);

    public async Task<bool> TamperRecordedAsync(long labId)
    {
        await using var db = await connections.OpenAsync();
        return (await db.QueryAsync("SELECT 1 x FROM seal_tamper WHERE tbl='lab' AND row_id=$1", r => 1, labId)).Count > 0;
    }

    public Task AddTamperRecordAsync(long labId, double oldValue, long batchId) =>
        Exec("INSERT INTO seal_tamper(tbl,row_id,col,old_value,batch_id) VALUES('lab',$1,'temp_c',$2,$3)", labId, oldValue, batchId);

    async Task Exec(string sql, params object?[] args)
    {
        await using var db = await connections.OpenAsync();
        await db.ExecAsync(sql, args);
    }
}
