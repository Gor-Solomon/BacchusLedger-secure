using System.Text.Json.Nodes;
using BacchusLedger.Services.Assistant;

namespace BacchusLedger.Infrastructure.Ai;

public sealed class LedgerApiClient(IHttpClientFactory httpClients) : ILedgerApiClient
{
    public async Task<JsonNode?> GetAsync(string baseUrl, string path)
    {
        if (!path.StartsWith("/api/")) throw new ArgumentException("read-only tools may only GET /api/ paths");
        using var resp = await httpClients.CreateClient("local").GetAsync(baseUrl + path);
        var text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) return new JsonObject { ["error"] = resp.StatusCode == System.Net.HttpStatusCode.NotFound ? "not found" : $"HTTP {(int)resp.StatusCode}" };
        return JsonNode.Parse(text);
    }
}
