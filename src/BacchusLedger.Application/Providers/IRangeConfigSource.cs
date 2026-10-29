using System.Text.Json.Nodes;

namespace BacchusLedger.Application.Providers;

/// Allowed min/max per measurement (config/ranges.json, DEMO ASSUMPTIONS); null when the file is missing or unreadable.
public interface IRangeConfigSource
{
    JsonNode? Config { get; }
}
