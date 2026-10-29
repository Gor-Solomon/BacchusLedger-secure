using System.Text.Json;
using BacchusLedger.Contracts.Assistant;

namespace BacchusLedger.Services;

public sealed class AssistantService : IAssistantService
{
    public AssistantStatusDto Status(string? lang) => Assistant.Status(AssistantConfig.FromEnv(), Lang(lang));

    public Task<ServiceResult<AssistantAnswer>> QuickAsync(int n, string? lang) => Task.FromResult(n is < 1 or > 5
        ? ServiceResult<AssistantAnswer>.NotFound("no such question")
        : ServiceResult<AssistantAnswer>.Ok(Assistant.Quick(n, Lang(lang), Db.Settings())));

    public Task<AssistantAnswer> AskAsync(JsonElement body, string localBaseUrl) =>
        Assistant.AskSafe(body, AssistantConfig.FromEnv(), Assistant.LocalGet(localBaseUrl));

    static string Lang(string? l) => l == "ka" ? "ka" : "en";
}
