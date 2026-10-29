using BacchusLedger.Application.Contracts.Overview;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BacchusLedger.Infrastructure.Persistence;

public sealed class EfWineryRepository(BacchusLedgerDbContext db) : IWineryRepository
{
    public Task<List<WineryDto>> ListAsync() => Project(db.Wineries.AsNoTracking().OrderBy(w => w.Code));

    public Task<List<WineryDto>> ListBySizeAsync() => Project(db.Wineries.AsNoTracking().OrderBy(w => w.YearlyT));

    public Task InsertAsync(string code, string name, string size, double yearlyT, bool usesQvevri, string readinessLevel) =>
        db.InsertAsync(new WineryEntity { Code = code, Name = name, Size = size, YearlyT = yearlyT, UsesQvevri = usesQvevri ? 1 : 0, ReadinessLevel = readinessLevel });

    static Task<List<WineryDto>> Project(IQueryable<WineryEntity> q) =>
        q.Select(w => new WineryDto(w.Id, w.Code, w.Name, w.Size, w.YearlyT, w.UsesQvevri, w.ReadinessLevel)).ToListAsync();
}
