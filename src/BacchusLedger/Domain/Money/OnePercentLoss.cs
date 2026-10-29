namespace BacchusLedger.Domain.Money;

/// "What 1% of loss costs per year" = yearly tonnes × litres per tonne × 1% × value per litre.
public sealed class OnePercentLoss(double valuePerLitre, double litresPerTonne)
{
    /// Litres that 1% of a year's harvest makes, 1 decimal.
    public double Litres(double yearlyTonnes) => Math.Round(yearlyTonnes * litresPerTonne * 0.01, 1);

    /// Value of those litres in USD, whole dollars.
    public double Usd(double yearlyTonnes) => Math.Round(yearlyTonnes * litresPerTonne * 0.01 * valuePerLitre, 0, MidpointRounding.AwayFromZero);
}
