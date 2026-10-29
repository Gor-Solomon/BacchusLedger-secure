namespace BacchusLedger.Domain.Readings;

/// Calculations for one reading given the Initial Brix of its vessel/batch.
public sealed class ReadingCalculator
{
    public ReadingCalculation Calculate(string type, double value, double? initialBrix)
    {
        if (type == "initial_brix")
        {
            var b = new InitialBrix(value);
            return new(b.PotentialAbv, Math.Round(b.SpecificGravity, 4), null, ReadingTypes.FormulaPotential + " · " + ReadingTypes.FormulaSg);
        }
        if (type == "hydrometer_abv" && initialBrix is { } brix && new InitialBrix(brix) is { PotentialAbv: > 0 } start)
            return new(start.PotentialAbv, null, start.ProgressPct(value), ReadingTypes.FormulaProgress + " · " + ReadingTypes.FormulaPotential);
        return new(null, null, null, null);
    }
}
