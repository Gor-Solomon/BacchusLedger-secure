using System.Text.Json;
using System.Text.Json.Nodes;
using BacchusLedger.Services.Assistant;

namespace BacchusLedger.Infrastructure.Ai;

/// Shared HTTP handling of the AI providers.
public abstract class AiProviderBase(IHttpClientFactory httpClients) : IAiProvider
{
    protected const int MaxRounds = 6;

    public abstract string Name { get; }

    public abstract Task<string> AnswerAsync(string question, IReadOnlyList<AssistantToolDef> tools, Func<string, JsonObject, Task<string>> runTool, CancellationToken ct);

    /// One retry after 1.5 s on HTTP 429 (per-minute rate limit), still inside the time budget.
    protected async Task<JsonNode> SendAsync(Func<HttpRequestMessage> make, CancellationToken ct)
    {
        var http = httpClients.CreateClient("ai");
        for (int attempt = 0; ; attempt++)
        {
            using var req = make();
            using var resp = await http.SendAsync(req, ct);
            var text = await resp.Content.ReadAsStringAsync(ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt == 0) { await Task.Delay(1500, ct); continue; }
            if (!resp.IsSuccessStatusCode) throw new AssistantException($"HTTP {(int)resp.StatusCode}");
            return JsonNode.Parse(text) ?? throw new AssistantException("empty response");
        }
    }

    /// Tool-call arguments: an object, or a JSON string holding one.
    protected static JsonObject Args(JsonNode? n) => n switch
    {
        JsonObject o => o,
        JsonValue v when v.GetValueKind() == JsonValueKind.String && JsonNode.Parse(v.GetValue<string>() is { Length: > 0 } s ? s : "{}") is JsonObject o2 => o2,
        _ => [],
    };
}
