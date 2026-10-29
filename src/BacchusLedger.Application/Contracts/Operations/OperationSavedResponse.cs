using BacchusLedger.Application.Contracts.Batches;

namespace BacchusLedger.Application.Contracts.Operations;

public sealed record OperationSavedResponse(long Id, StageDto? Stage);
