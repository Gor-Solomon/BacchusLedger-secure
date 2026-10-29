using BacchusLedger.Contracts.Batches;

namespace BacchusLedger.Contracts.Operations;

public sealed record OperationPreviewResponse(
    StageDto Stage,
    double? Litres,
    string? Method,
    DipInfoDto? Dip,
    IReadOnlyList<CheckDto> Checks,
    OperationTotalsDto Totals);
