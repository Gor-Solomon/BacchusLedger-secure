using BacchusLedger.Contracts.Journals;

namespace BacchusLedger.Services;

public sealed class JournalService : IJournalService
{
    public Task<IReadOnlyList<JournalCoverageDto>> CoverageAsync(long? batch, string? winery)
    {
        using var c = Db.Open();
        IReadOnlyList<JournalCoverageDto> list = Enumerable.Range(1, 8).Select(n =>
        {
            var j = Journals.Build(c, n, batch, winery);
            return new JournalCoverageDto(n, j.Title, j.Rows.Count, j.Rows.Count > 0, n == 8);
        }).ToList();
        return Task.FromResult(list);
    }

    public Task<ServiceResult<JournalDto>> GetAsync(int n, long? batch, string? winery)
    {
        if (n is < 1 or > 8) return Task.FromResult(ServiceResult<JournalDto>.NotFound());
        using var c = Db.Open();
        var j = Journals.Build(c, n, batch, winery);
        return Task.FromResult<ServiceResult<JournalDto>>(new JournalDto(j.N, j.Title, j.Note,
            j.Columns.Select(x => new JournalColumnDto(x.Key, x.Label, x.Source)).ToList(), j.Rows.Select(r => (IReadOnlyDictionary<string, object?>)r).ToList()));
    }

    public Task<ServiceResult<JournalCsvFile>> CsvAsync(int n, long? batch, string? winery)
    {
        if (n is < 1 or > 8) return Task.FromResult(ServiceResult<JournalCsvFile>.NotFound());
        using var c = Db.Open();
        var j = Journals.Build(c, n, batch, winery);
        return Task.FromResult<ServiceResult<JournalCsvFile>>(new JournalCsvFile(Journals.Csv(j), $"journal-{n}-{j.Title.ToLowerInvariant().Replace(' ', '-')}.csv"));
    }
}
