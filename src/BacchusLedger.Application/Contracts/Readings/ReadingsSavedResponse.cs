namespace BacchusLedger.Application.Contracts.Readings;

public sealed record ReadingsSavedResponse(IReadOnlyList<ReadingResultDto> Saved);
