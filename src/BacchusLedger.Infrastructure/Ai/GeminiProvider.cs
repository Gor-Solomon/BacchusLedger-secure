using System.Text;
using System.Text.Json.Nodes;
using BacchusLedger.Application.Options;
using BacchusLedger.Application.Services.Assistant;
using Microsoft.Extensions.Options;

namespace BacchusLedger.Infrastructure.Ai;

public sealed class GeminiProvider(IHttpClientFactory httpClients, IOptions<AssistantOptions> options) : AiProviderBase(httpClients)
{
    public override string Name => "gemini";

    public override async Task<string> AnswerAsync(string q, IReadOnlyList<AssistantToolDef> tools, Func<string, JsonObject, Task<string>> runTool, CancellationToken ct)
    {
        var cfg = options.Value;
        var contents = new JsonArray { new JsonObject { ["role"] = "user", ["parts"] = new JsonArray { new JsonObject { ["text"] = q } } } };
        var decls = new JsonArray(tools.Select(d =>
        {
            var o = new JsonObject { ["name"] = d.Name, ["description"] = d.Description };
            if (d.Params.Length > 0) o["parameters"] = d.Schema();
            return (JsonNode)o;
        }).ToArray());
        for (int round = 0; round < MaxRounds; round++)
        {
            var body = new JsonObject
            {
                ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray { new JsonObject { ["text"] = AssistantTexts.Prompt(q) } } },
                ["contents"] = contents.DeepClone(),
            };
            // Last round: no tools at all (Gemini may ignore mode NONE), so it must answer from what the tools already returned.
            if (round < MaxRounds - 1) body["tools"] = new JsonArray { new JsonObject { ["functionDeclarations"] = decls.DeepClone() } };
            HttpRequestMessage Make()
            {
                var req = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(cfg.GeminiModel)}:generateContent")
                { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
                req.Headers.Add("x-goog-api-key", cfg.GeminiKey); // header, not URL: keys never appear in URLs or logs
                return req;
            }
            var content = (await SendAsync(Make, ct))["candidates"]?[0]?["content"] ?? throw new AssistantException("no candidate");
            var parts = content["parts"] as JsonArray ?? [];
            var calls = parts.Where(p => p?["functionCall"] != null).ToList();
            if (calls.Count == 0) return string.Concat(parts.Where(p => p?["thought"]?.GetValue<bool>() != true).Select(p => (string?)p?["text"] ?? ""));
            contents.Add(content.DeepClone());
            var replies = new JsonArray();
            foreach (var p in calls)
            {
                var fc = p!["functionCall"]!;
                var json = await runTool((string)fc["name"]!, Args(fc["args"]));
                var fr = new JsonObject { ["name"] = (string)fc["name"]!, ["response"] = new JsonObject { ["result"] = json } };
                if (fc["id"] is { } id) fr["id"] = id.DeepClone();
                replies.Add(new JsonObject { ["functionResponse"] = fr });
            }
            contents.Add(new JsonObject { ["role"] = "user", ["parts"] = replies });
        }
        throw new AssistantException("too many tool rounds");
    }
}
