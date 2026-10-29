namespace BacchusLedger.Contracts.Readings;

public sealed record ReadingsSavedResponse(IReadOnlyList<ReadingResultDto> Saved);
