using System.Text.Json;
using BacchusLedger.Contracts.Assistant;

namespace BacchusLedger.Services;

/// Marani Assistant (read-only): quick questions answered by our own code, free questions via AI tool calling over our own GET API.
public interface IAssistantService
{
    AssistantStatusDto Status(string? lang);

    Task<ServiceResult<AssistantAnswer>> QuickAsync(int n, string? lang);

    /// Never fails: errors become an "offline" answer. localBaseUrl is this server, used by the read-only tools.
    Task<AssistantAnswer> AskAsync(JsonElement body, string localBaseUrl);

    /// The free question with the given options (the selftest passes "no keys").
    Task<AssistantAnswer> AskAsync(string? question, Options.AssistantOptions options, Func<string, Task<System.Text.Json.Nodes.JsonNode?>> get);
}
