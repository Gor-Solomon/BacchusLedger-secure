using System.Text.Json;
using BacchusLedger.Application.Contracts.Operations;

namespace BacchusLedger.Application.Services;

public interface IOperationService
{
    /// What the operation would do (stage, loss, checks) without saving it.
    Task<ServiceResult<OperationPreviewResponse>> PreviewAsync(JsonElement body);

    /// Appends the operation to the record (operations are append-only).
    Task<ServiceResult<OperationSavedResponse>> RecordAsync(JsonElement body);
}
