using System.Text.Json.Nodes;

namespace BacchusLedger.Application.Services.Assistant;

/// Read-only tools for the AI. Each one GETs our own API and returns a small JSON object.
public interface IAssistantTools
{
    IReadOnlyList<AssistantToolDef> Defs { get; }

    /// Runs one tool. Returns the JSON given to the model and a short "Used:" label for the chat.
    Task<(string Json, string Used)> RunAsync(string name, JsonObject args, Func<string, Task<JsonNode?>> get);
}
