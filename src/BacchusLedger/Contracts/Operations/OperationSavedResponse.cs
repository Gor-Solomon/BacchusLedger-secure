using BacchusLedger.Contracts.Batches;

namespace BacchusLedger.Contracts.Operations;

public sealed record OperationSavedResponse(long Id, StageDto? Stage);
