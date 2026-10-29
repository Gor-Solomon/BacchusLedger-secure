using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using BacchusLedger.Application.Options;
using BacchusLedger.Application.Services.Assistant;
using Microsoft.Extensions.Options;

namespace BacchusLedger.Infrastructure.Ai;

public sealed class OpenAiProvider(IHttpClientFactory httpClients, IOptions<AssistantOptions> options) : AiProviderBase(httpClients)
{
    public override string Name => "openai";

    public override async Task<string> AnswerAsync(string q, IReadOnlyList<AssistantToolDef> tools, Func<string, JsonObject, Task<string>> runTool, CancellationToken ct)
    {
        var cfg = options.Value;
        var messages = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = AssistantTexts.Prompt(q) }, new JsonObject { ["role"] = "user", ["content"] = q } };
        var toolArray = new JsonArray(tools.Select(d => (JsonNode)new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject { ["name"] = d.Name, ["description"] = d.Description, ["parameters"] = d.Schema() },
        }).ToArray());
        for (int round = 0; round < MaxRounds; round++)
        {
            var body = new JsonObject { ["model"] = cfg.OpenAiModel, ["messages"] = messages.DeepClone(), ["tools"] = toolArray.DeepClone() };
            if (cfg.OpenAiModel.StartsWith("gpt-5") || cfg.OpenAiModel.StartsWith('o')) body["reasoning_effort"] = "low";
            if (round == MaxRounds - 1) body["tool_choice"] = "none";
            HttpRequestMessage Make()
            {
                var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions") { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cfg.OpenAiKey);
                return req;
            }
            var msg = (await SendAsync(Make, ct))["choices"]?[0]?["message"] ?? throw new AssistantException("no message");
            if (msg["tool_calls"] is not JsonArray { Count: > 0 } calls) return (string?)msg["content"] ?? "";
            messages.Add(msg.DeepClone());
            foreach (var call in calls)
            {
                var json = await runTool((string)call!["function"]!["name"]!, Args(call["function"]!["arguments"]));
                messages.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = (string)call["id"]!, ["content"] = json });
            }
        }
        throw new AssistantException("too many tool rounds");
    }
}
