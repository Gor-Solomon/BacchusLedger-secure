using System.Text.Json.Nodes;

namespace BacchusLedger.Services.Assistant;

/// The assistant's only way to the data: GET requests to our own /api. It never writes.
public interface ILedgerApiClient
{
    /// GET baseUrl + path (path must start with /api/). Errors come back as { "error": … }.
    Task<JsonNode?> GetAsync(string baseUrl, string path);
}
