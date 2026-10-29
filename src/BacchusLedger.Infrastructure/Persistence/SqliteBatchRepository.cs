using BacchusLedger.Application.Models;
using BacchusLedger.Application.Repositories;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class SqliteBatchRepository(ISqliteConnectionFactory connections) : IBatchRepository
{
    const string Select = "SELECT b.*, w.code AS winery_code, w.name AS winery_name FROM batches b JOIN wineries w ON w.id=b.winery_id";

    public async Task<BatchRecord?> GetAsync(long id) => (await Batches(Select + " WHERE b.id=$1", id)).FirstOrDefault();

    public async Task<BatchRecord?> GetByCodeAsync(string code) => (await Batches(Select + " WHERE b.code=$1", code)).FirstOrDefault();

    public async Task<long?> FindIdAsync(string code) => (await Ids("SELECT id FROM batches WHERE code=$1", code)).Cast<long?>().FirstOrDefault();

    public Task<List<BatchRecord>> ListAsync() => Batches(Select + " ORDER BY b.code");

    public Task<List<BatchRecord>> ListRealAsync() => Batches(Select + " WHERE COALESCE(b.source_note,'') NOT LIKE '%copy%'");

    public Task<List<long>> ListRealIdsAsync() => Ids("SELECT id FROM batches WHERE COALESCE(source_note,'') NOT LIKE '%copy%'");

    public async Task<List<BatchRef>> ListRefsAsync()
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync("SELECT b.id, b.code, b.source_note, w.code AS winery FROM batches b JOIN wineries w ON w.id=b.winery_id",
            r => new BatchRef(r.Long("id"), r.Str("code")!, r.Str("source_note"), r.Str("winery")!));
    }

    public Task<List<long>> ListIdsAsync() => Ids("SELECT id FROM batches ORDER BY id");

    public Task<List<long>> ListIdsAsync(long? batch, string? winery)
    {
        var (where, args) = new RecordFilter(batch, winery).ForBatches("b");
        return Ids($"SELECT id FROM batches b WHERE {where} ORDER BY b.code", args);
    }

    public async Task<long> CountCodesLikeAsync(string pattern)
    {
        await using var db = await connections.OpenAsync();
        return await db.CountAsync("SELECT COUNT(*) n FROM batches WHERE code LIKE $1", pattern);
    }

    public async Task<long> InsertAsync(NewBatch b)
    {
        await using var db = await connections.OpenAsync();
        return await db.InsertAsync("""
            INSERT INTO batches(winery_id,code,name,wine_type,technology,variety,vintage,abv,status,source_note,grapes_kg)
            VALUES((SELECT id FROM wineries WHERE code=$1),$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)
            """, b.WineryCode, b.Code, b.Name, b.WineType, b.Technology, b.Variety, b.Vintage, b.Abv, b.Status, b.SourceNote, b.GrapesKg);
    }

    public async Task SetStatusAsync(string code, string status)
    {
        await using var db = await connections.OpenAsync();
        await db.ExecAsync("UPDATE batches SET status=$1 WHERE code=$2", status, code);
    }

    public async Task DeleteAsync(long id)
    {
        await using var db = await connections.OpenAsync();
        await db.ExecAsync("DELETE FROM batches WHERE id=$1", id);
    }

    async Task<List<BatchRecord>> Batches(string sql, params object?[] args)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync(sql, r => new BatchRecord(r.Long("id"), r.Str("code")!, r.Str("name"), r.Str("wine_type"), r.Str("technology"), r.Str("variety"),
            r.LongOrNull("vintage"), r.Dbl("abv"), r.Str("status"), r.Str("source_note"), r.Dbl("grapes_kg"), r.Str("winery_code")!, r.Str("winery_name")), args);
    }

    async Task<List<long>> Ids(string sql, params object?[] args)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync(sql, r => r.Long("id"), args);
    }
}
