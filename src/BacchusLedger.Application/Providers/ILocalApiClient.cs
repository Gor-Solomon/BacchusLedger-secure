using System.Text.Json.Nodes;

namespace BacchusLedger.Application.Providers;

/// HTTP calls to this server's own API (assistant tools and selftest). Read-only except the assistant's empty question.
public interface ILocalApiClient
{
    /// GET baseUrl + path as JSON (path must start with /api/). Errors come back as { "error": … }.
    Task<JsonNode?> GetJsonAsync(string baseUrl, string path);

    /// GET baseUrl + path as text; throws on an HTTP error.
    Task<string> GetStringAsync(string baseUrl, string path);

    /// POST a JSON body; returns the status code and the response text.
    Task<(int Status, string Body)> PostJsonAsync(string baseUrl, string path, string json);

    /// The media type of the response headers (the body is not read).
    Task<string> GetContentTypeAsync(string baseUrl, string path);
}
