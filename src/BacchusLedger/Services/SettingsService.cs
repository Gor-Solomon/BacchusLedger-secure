using System.Globalization;
using System.Text.Json;
using BacchusLedger.Contracts.Settings;
using BacchusLedger.Dataset;
using BacchusLedger.Repositories;

namespace BacchusLedger.Services;

public sealed class SettingsService(ISettingsRepository settings, IUnitOfWork unitOfWork, IDatabaseInitializer database, ILiveControl live) : ISettingsService
{
    public async Task<IReadOnlyDictionary<string, string>> GetAsync() => await settings.GetAsync();

    public async Task<ServiceResult<IReadOnlyDictionary<string, string>>> SaveAsync(IReadOnlyDictionary<string, JsonElement> values)
    {
        foreach (var (k, v) in values)
        {
            if (!SettingDefaults.Values.ContainsKey(k)) return ServiceResult<IReadOnlyDictionary<string, string>>.BadRequest($"unknown setting {k}");
            if (k != "language" && !double.TryParse(Text(v) ?? "", NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                return ServiceResult<IReadOnlyDictionary<string, string>>.BadRequest($"{k} must be a number");
        }
        await unitOfWork.WriteAsync(async () => { await settings.SaveAsync(values.Select(kv => new KeyValuePair<string, string?>(kv.Key, Text(kv.Value)))); return 0; });
        var saved = await settings.GetAsync();
        live.UseSettings(saved);
        return ServiceResult<IReadOnlyDictionary<string, string>>.Ok(saved);
    }

    public async Task<ResetResponse> ResetAsync()
    {
        await database.ResetAndSeedAsync();
        return new ResetResponse(true);
    }

    static string? Text(JsonElement v) => v.ValueKind == JsonValueKind.Number ? v.GetDouble().ToString(CultureInfo.InvariantCulture) : v.GetString();
}
