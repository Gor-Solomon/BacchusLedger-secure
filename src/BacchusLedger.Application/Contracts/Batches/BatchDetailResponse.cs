namespace BacchusLedger.Application.Contracts.Batches;

public sealed record BatchDetailResponse(
    BatchSummaryDto Batch,
    IReadOnlyList<StageDto> Stages,
    IReadOnlyList<CheckDto> Checks,
    IReadOnlyList<BatchOriginDto> Origins,
    IReadOnlyList<LabRowDto> Lab,
    BatchTotalsDto Totals,
    IReadOnlyDictionary<string, string> Settings);
