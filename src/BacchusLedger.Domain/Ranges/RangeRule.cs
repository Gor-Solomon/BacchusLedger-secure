using System.Globalization;

namespace BacchusLedger.Domain.Ranges;

/// Allowed min/max of one measurement; From says where it came from ("default …" or "override …").
public sealed record RangeRule(double Min, double Max, string Unit, string From)
{
    /// A limit or value as shown in alerts (up to 3 decimals, invariant).
    public static string Format(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
