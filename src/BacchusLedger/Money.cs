using BacchusLedger.Contracts.Overview;

namespace BacchusLedger;

public static class Money
{
    /// "What 1% of loss costs per year" = yearly_t × litres_per_tonne × 1% × value_per_litre (wineries in the given order).
    public static List<OnePercentRow> OnePercentTable(IEnumerable<WineryDto> wineries, double vpl, double lpt) =>
        wineries.Select(w =>
        {
            double t = w.YearlyT ?? 0;
            double v = Math.Round(t * lpt * 0.01 * vpl, 0, MidpointRounding.AwayFromZero);
            return new OnePercentRow(w.Code!, w.Name, w.Size, t, Math.Round(t * lpt * 0.01, 1), v, w.Code == "W01");
        }).ToList();
}
