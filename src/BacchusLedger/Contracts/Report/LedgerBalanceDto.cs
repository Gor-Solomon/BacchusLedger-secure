namespace BacchusLedger.Contracts.Report;

public sealed record LedgerBalanceDto(
    double? GrapesKg,
    double HarvestedKg,
    double PressedKg,
    double GrapesLeftKg,
    double? KgGap,
    double InL,
    double BottledL,
    double InVesselsL,
    double LossesL,
    double GapL,
    bool HasLitres,
    double EngineTotalLossL,
    bool EngineMatches,
    bool Balanced,
    IReadOnlyList<BalanceCheckDto> Checks);
