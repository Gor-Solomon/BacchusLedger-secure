using System.Text;
using System.Text.Json.Nodes;
using BacchusLedger.Application.Providers;

namespace BacchusLedger.Infrastructure.Providers;

public sealed class LocalApiClient(IHttpClientFactory httpClients) : ILocalApiClient
{
    public async Task<JsonNode?> GetJsonAsync(string baseUrl, string path)
    {
        if (!path.StartsWith("/api/")) throw new ArgumentException("read-only tools may only GET /api/ paths");
        using var resp = await httpClients.CreateClient("local").GetAsync(baseUrl + path);
        var text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) return new JsonObject { ["error"] = resp.StatusCode == System.Net.HttpStatusCode.NotFound ? "not found" : $"HTTP {(int)resp.StatusCode}" };
        return JsonNode.Parse(text);
    }

    public Task<string> GetStringAsync(string baseUrl, string path) => httpClients.CreateClient("local").GetStringAsync(baseUrl + path);

    public async Task<(int Status, string Body)> PostJsonAsync(string baseUrl, string path, string json)
    {
        using var resp = await httpClients.CreateClient("local").PostAsync(baseUrl + path, new StringContent(json, Encoding.UTF8, "application/json"));
        return ((int)resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    public async Task<string> GetContentTypeAsync(string baseUrl, string path)
    {
        using var resp = await httpClients.CreateClient("local").SendAsync(new HttpRequestMessage(HttpMethod.Get, baseUrl + path), HttpCompletionOption.ResponseHeadersRead);
        return resp.Content.Headers.ContentType?.MediaType ?? "none";
    }
}
