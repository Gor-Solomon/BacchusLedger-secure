using System.Text.Json.Nodes;
using BacchusLedger.Providers;
using Microsoft.Extensions.Logging;

namespace BacchusLedger.Infrastructure.Providers;

/// Reads config/ranges.json next to the app once.
public sealed class FileRangeConfigSource : IRangeConfigSource
{
    readonly Lazy<JsonNode?> config;

    public FileRangeConfigSource(ILogger<FileRangeConfigSource> logger)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "config", "ranges.json");
        config = new(() =>
        {
            try { return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) : null; }
            catch (Exception e) { logger.LogError("ranges: config/ranges.json unreadable ({Error}) — no range alerts", e.GetType().Name); return null; }
        });
    }

    public JsonNode? Config => config.Value;
}
