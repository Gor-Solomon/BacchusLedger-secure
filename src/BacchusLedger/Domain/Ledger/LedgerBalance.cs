namespace BacchusLedger.Domain.Ledger;

/// In = out + losses, in kg and litres, and whether it matches the loss calculator.
public sealed record LedgerBalance(double? GrapesKg, double HarvestedKg, double PressedKg, double GrapesLeftKg, double? KgGap,
    double InL, double BottledL, double InVesselsL, double LossesL, double GapL, bool HasLitres, bool EngineMatches, bool Balanced);
