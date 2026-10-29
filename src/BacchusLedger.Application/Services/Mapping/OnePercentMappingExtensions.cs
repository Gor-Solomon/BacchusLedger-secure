using BacchusLedger.Application.Contracts.Overview;
using BacchusLedger.Domain.Money;

namespace BacchusLedger.Application.Services.Mapping;

public static class OnePercentMappingExtensions
{
    /// One row per winery, in the given order; W01 is shown as "up to".
    public static List<OnePercentRow> ToRows(this OnePercentLoss loss, IEnumerable<WineryDto> wineries) => wineries.Select(w =>
    {
        double t = w.YearlyT ?? 0;
        return new OnePercentRow(w.Code!, w.Name, w.Size, t, loss.Litres(t), loss.Usd(t), w.Code == "W01");
    }).ToList();
}
