using System.Globalization;
using System.Text.Json;
using BacchusLedger.Contracts.Settings;

namespace BacchusLedger.Services;

public sealed class SettingsService(LiveService live) : ISettingsService
{
    public Task<IReadOnlyDictionary<string, string>> GetAsync() => Task.FromResult<IReadOnlyDictionary<string, string>>(Db.Settings());

    public Task<ServiceResult<IReadOnlyDictionary<string, string>>> SaveAsync(IReadOnlyDictionary<string, JsonElement> values)
    {
        foreach (var (k, v) in values)
        {
            if (!Db.DefaultSettings.ContainsKey(k)) return Bad($"unknown setting {k}");
            if (k != "language" && !double.TryParse(Text(v) ?? "", NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return Bad($"{k} must be a number");
        }
        Db.Write((c, tx) =>
        {
            foreach (var (k, v) in values)
                Db.Exec(c, tx, "INSERT OR REPLACE INTO settings(key,value) VALUES($1,$2)", k, Text(v));
            return 0;
        });
        var saved = Db.Settings();
        live.Sim.UseSettings(saved);
        return Task.FromResult(ServiceResult<IReadOnlyDictionary<string, string>>.Ok(saved));

        static Task<ServiceResult<IReadOnlyDictionary<string, string>>> Bad(string e) => Task.FromResult(ServiceResult<IReadOnlyDictionary<string, string>>.BadRequest(e));
    }

    public Task<ResetResponse> ResetAsync()
    {
        Db.Reseed();
        return Task.FromResult(new ResetResponse(true));
    }

    static string? Text(JsonElement v) => v.ValueKind == JsonValueKind.Number ? v.GetDouble().ToString(CultureInfo.InvariantCulture) : v.GetString();
}
