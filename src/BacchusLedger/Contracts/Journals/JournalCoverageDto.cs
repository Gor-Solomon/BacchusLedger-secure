namespace BacchusLedger.Contracts.Journals;

public sealed record JournalCoverageDto(int N, string Title, int Rows, bool Filled, bool Template);
