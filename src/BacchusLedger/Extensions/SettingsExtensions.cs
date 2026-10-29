using System.Globalization;
using BacchusLedger.Dataset;
using BacchusLedger.Domain.Losses;

namespace BacchusLedger.Extensions;

public static class SettingsExtensions
{
    /// A numeric setting (the default when the key is missing).
    public static double Number(this IReadOnlyDictionary<string, string> settings, string key) =>
        double.Parse(settings.TryGetValue(key, out var v) ? v : SettingDefaults.Values[key], CultureInfo.InvariantCulture);

    /// Normal loss per stage from the norm_* settings.
    public static StageNorms Norms(this IReadOnlyDictionary<string, string> settings) =>
        new(StageNorms.Keys.ToDictionary(k => k, k => settings.Number("norm_" + k)));
}
