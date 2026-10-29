using System.Text.Json.Nodes;

namespace BacchusLedger.Services.Assistant;

/// A read-only tool the AI may call.
public sealed record AssistantToolDef(string Name, string Description, AssistantToolParam[] Params)
{
    /// JSON schema of the parameters.
    public JsonObject Schema()
    {
        var props = new JsonObject();
        foreach (var p in Params) props[p.Name] = new JsonObject { ["type"] = p.Type, ["description"] = p.Description };
        return new JsonObject { ["type"] = "object", ["properties"] = props, ["required"] = new JsonArray(Params.Where(p => p.Required).Select(p => (JsonNode)p.Name).ToArray()) };
    }
}
