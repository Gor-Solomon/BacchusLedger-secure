namespace BacchusLedger.Application.Contracts.Readings;

public sealed record ReadingPreviewResponse(IReadOnlyList<ReadingResultDto> Items);
