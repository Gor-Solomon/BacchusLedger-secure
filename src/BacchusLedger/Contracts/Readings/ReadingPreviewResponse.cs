namespace BacchusLedger.Contracts.Readings;

public sealed record ReadingPreviewResponse(IReadOnlyList<ReadingResultDto> Items);
