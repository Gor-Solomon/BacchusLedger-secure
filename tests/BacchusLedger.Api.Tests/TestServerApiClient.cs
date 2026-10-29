using System.Text;
using System.Text.Json.Nodes;
using BacchusLedger.Application.Providers;

namespace BacchusLedger.Api.Tests;

/// ILocalApiClient on the test server (the base URL is ignored: there is only one server).
public sealed class TestServerApiClient(Func<HttpClient> client) : ILocalApiClient
{
    public async Task<JsonNode?> GetJsonAsync(string baseUrl, string path)
    {
        using var resp = await client().GetAsync(path);
        var text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) return new JsonObject { ["error"] = resp.StatusCode == System.Net.HttpStatusCode.NotFound ? "not found" : $"HTTP {(int)resp.StatusCode}" };
        return JsonNode.Parse(text);
    }

    public Task<string> GetStringAsync(string baseUrl, string path) => client().GetStringAsync(path);

    public async Task<(int Status, string Body)> PostJsonAsync(string baseUrl, string path, string json)
    {
        using var resp = await client().PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"));
        return ((int)resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    public async Task<string> GetContentTypeAsync(string baseUrl, string path)
    {
        using var resp = await client().SendAsync(new HttpRequestMessage(HttpMethod.Get, path), HttpCompletionOption.ResponseHeadersRead);
        return resp.Content.Headers.ContentType?.MediaType ?? "none";
    }
}
