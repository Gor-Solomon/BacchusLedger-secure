using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BacchusLedger.Contracts.Assistant;
using BacchusLedger.Options;
using BacchusLedger.Repositories;
using BacchusLedger.Services.Assistant;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static BacchusLedger.Services.Assistant.AssistantTexts;

namespace BacchusLedger.Services;

public sealed class AssistantService(
    IOptions<AssistantOptions> options, IQuickAnswerService quick, ISettingsRepository settings, IAssistantTools tools, IEnumerable<IAiProvider> providers,
    ILedgerApiClient api, ILogger<AssistantService> logger) : IAssistantService
{
    const int TimeoutSeconds = 15;
    static readonly Regex KeyLike = new(@"(?:sk-|AQ\.)[A-Za-z0-9_\-]{8,}");

    public AssistantStatusDto Status(string? lang)
    {
        var l = Lang(lang);
        var cfg = options.Value;
        return new(cfg.Configured, cfg.Configured ? L(l, "AI on · answers only from the ledger", "AI ჩართულია · პასუხობს მხოლოდ ჟურნალიდან") : NotConfigured(l),
            Greeting(l), MaxChars, QuickTitles.Select((q, i) => new QuickQuestionDto(i + 1, L(l, q[0], q[1]))).ToList());
    }

    public async Task<ServiceResult<AssistantAnswer>> QuickAsync(int n, string? lang) => n is < 1 or > 5
        ? ServiceResult<AssistantAnswer>.NotFound("no such question")
        : await quick.AnswerAsync(n, Lang(lang), await settings.GetAsync());

    public async Task<AssistantAnswer> AskAsync(JsonElement body, string localBaseUrl)
    {
        var q = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("question", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        try { return await AskAsync(q, options.Value, path => api.GetAsync(localBaseUrl, path)); }
        catch (Exception e)
        {
            logger.LogError("assistant: ask failed ({Error})", e.GetType().Name);
            return new AssistantAnswer { Mode = "offline", Text = Offline(IsKa(q) ? "ka" : "en") };
        }
    }

    public async Task<AssistantAnswer> AskAsync(string? question, AssistantOptions cfg, Func<string, Task<JsonNode?>> get)
    {
        var q = (question ?? "").Trim();
        var lang = IsKa(q) ? "ka" : "en";
        if (q.Length == 0) return new() { Mode = "invalid", Text = L(lang, $"Type a question (max {MaxChars} characters).", $"დაწერეთ კითხვა (მაქს. {MaxChars} სიმბოლო).") };
        if (q.Length > MaxChars) q = q[..MaxChars];
        if (!cfg.Configured) return new() { Mode = "not_configured", Text = NotConfigured(lang) };

        foreach (var p in cfg.Order())
        {
            var provider = providers.First(x => x.Name == p);
            var used = new List<string>();
            var outputs = new List<string>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            try
            {
                var text = (await provider.AnswerAsync(q, tools.Defs, async (name, args) =>
                {
                    var (json, label) = await tools.RunAsync(name, args, get);
                    used.Add(label); outputs.Add(json);
                    return json;
                }, cts.Token)).Trim();
                if (text.Length == 0) throw new AssistantException("empty answer");
                text = Scrub(cfg, text.Replace("**", ""));
                // Enforced: every number in the answer must come from a tool result, otherwise no answer.
                if (!AnswerGuard.NumbersFromTools(text, outputs)) { logger.LogWarning("assistant: {Provider} answer used numbers not returned by tools — replaced", p); text = NoData(lang); }
                return new() { Mode = "ai", Provider = p, Text = text, Used = used.Distinct().ToList(), Sources = AnswerGuard.Tags(text) };
            }
            catch (Exception e)
            {
                // Only the provider name and error type/status are logged, never bodies (providers can echo part of a key).
                logger.LogError("assistant: {Provider} failed ({Error}); tools: {Tools}", p,
                    e is OperationCanceledException ? "timeout" : e is AssistantException ? e.Message : e.GetType().Name, string.Join(", ", used));
            }
        }
        return new() { Mode = "offline", Text = Offline(lang) };
    }

    /// Belt and braces: remove anything key-shaped from text that goes to the browser.
    static string Scrub(AssistantOptions cfg, string s)
    {
        foreach (var k in cfg.Keys()) s = s.Replace(k, "[removed]");
        return KeyLike.Replace(s, "[removed]");
    }

    static string Lang(string? l) => l == "ka" ? "ka" : "en";
}
