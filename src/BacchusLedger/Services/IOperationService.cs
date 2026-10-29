using System.Text.Json;
using BacchusLedger.Contracts.Operations;

namespace BacchusLedger.Services;

public interface IOperationService
{
    /// What the operation would do (stage, loss, checks) without saving it.
    Task<ServiceResult<OperationPreviewResponse>> PreviewAsync(JsonElement body);

    /// Appends the operation to the record (operations are append-only).
    Task<ServiceResult<OperationSavedResponse>> RecordAsync(JsonElement body);
}
