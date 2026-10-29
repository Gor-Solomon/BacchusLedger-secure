namespace BacchusLedger.Domain.Readings;

/// Sugar at the start of fermentation (°Bx) and what follows from it (section 4 formulas).
public sealed record InitialBrix(double Brix)
{
    /// potential ABV ≈ Initial Brix × 0.59 (rule of thumb), 2 decimals.
    public double PotentialAbv => Math.Round(Brix * 0.59, 2, MidpointRounding.AwayFromZero);

    /// SG = 1 + Brix / (258.6 − (Brix / 258.2) × 227.1).
    public double SpecificGravity => 1 + Brix / (258.6 - Brix / 258.2 * 227.1);

    /// Fermentation progress in % (ABV reached ÷ potential ABV), 1 decimal.
    public double ProgressPct(double abvReached) => Math.Round(abvReached / PotentialAbv * 100, 1, MidpointRounding.AwayFromZero);
}
