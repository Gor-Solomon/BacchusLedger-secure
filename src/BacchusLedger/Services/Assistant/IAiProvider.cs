using System.Text.Json.Nodes;

namespace BacchusLedger.Services.Assistant;

/// An AI chat provider with tool calling.
public interface IAiProvider
{
    /// "openai" or "gemini".
    string Name { get; }

    /// Asks the question; the provider may call the tools (runTool returns the tool's JSON) before it answers.
    Task<string> AnswerAsync(string question, IReadOnlyList<AssistantToolDef> tools, Func<string, JsonObject, Task<string>> runTool, CancellationToken ct);
}
