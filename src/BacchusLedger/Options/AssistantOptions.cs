namespace BacchusLedger.Options;

/// AI provider settings, read ONLY from environment variables (OPENAI_API_KEY, GEMINI_API_KEY, OPENAI_MODEL, GEMINI_MODEL,
/// ASSISTANT_PROVIDER, ASSISTANT_ENABLED). Keys never leave the server.
public sealed class AssistantOptions
{
    public string? OpenAiKey { get; set; }
    public string? GeminiKey { get; set; }
    public string OpenAiModel { get; set; } = "gpt-5.4-mini";
    public string GeminiModel { get; set; } = "gemini-3.5-flash-lite";

    /// "openai" or "gemini": the provider tried first.
    public string Provider { get; set; } = "openai";
    public bool Enabled { get; set; } = true;

    /// No keys, AI off (selftest).
    public static AssistantOptions None => new() { Enabled = false };

    /// Providers to try: the preferred one first, then the other once — only those with a key.
    public List<string> Order()
    {
        if (!Enabled) return [];
        string[] all = Provider == "gemini" ? ["gemini", "openai"] : ["openai", "gemini"];
        return all.Where(p => (p == "openai" ? OpenAiKey : GeminiKey) != null).ToList();
    }

    public bool Configured => Order().Count > 0;

    public IEnumerable<string> Keys() => new[] { OpenAiKey, GeminiKey }.Where(k => k is { Length: >= 8 })!;

    public override string ToString() => $"assistant provider={Provider} configured={Configured}"; // never the keys
}
