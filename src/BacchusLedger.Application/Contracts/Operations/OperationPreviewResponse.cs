using BacchusLedger.Application.Contracts.Batches;

namespace BacchusLedger.Application.Contracts.Operations;

public sealed record OperationPreviewResponse(
    StageDto Stage,
    double? Litres,
    string? Method,
    DipInfoDto? Dip,
    IReadOnlyList<CheckDto> Checks,
    OperationTotalsDto Totals);
