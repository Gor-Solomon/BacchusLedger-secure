using BacchusLedger.Application.Contracts.Overview;
using BacchusLedger.Application.Repositories;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class SqliteWineryRepository(ISqliteConnectionFactory connections) : IWineryRepository
{
    public Task<List<WineryDto>> ListAsync() => List("SELECT * FROM wineries ORDER BY code");

    public Task<List<WineryDto>> ListBySizeAsync() => List("SELECT * FROM wineries ORDER BY yearly_t");

    public async Task InsertAsync(string code, string name, string size, double yearlyT, bool usesQvevri, string readinessLevel)
    {
        await using var db = await connections.OpenAsync();
        await db.ExecAsync("INSERT INTO wineries(code,name,size,yearly_t,uses_qvevri,readiness_level) VALUES($1,$2,$3,$4,$5,$6)",
            code, name, size, yearlyT, usesQvevri ? 1 : 0, readinessLevel);
    }

    async Task<List<WineryDto>> List(string sql)
    {
        await using var db = await connections.OpenAsync();
        return await db.QueryAsync(sql, r => new WineryDto(r.Long("id"), r.Str("code"), r.Str("name"), r.Str("size"), r.Dbl("yearly_t"), r.LongOrNull("uses_qvevri"), r.Str("readiness_level")));
    }
}
