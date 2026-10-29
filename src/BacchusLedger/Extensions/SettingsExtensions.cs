using System.Globalization;
using BacchusLedger.Dataset;

namespace BacchusLedger.Extensions;

public static class SettingsExtensions
{
    /// A numeric setting (the default when the key is missing).
    public static double Number(this IReadOnlyDictionary<string, string> settings, string key) =>
        double.Parse(settings.TryGetValue(key, out var v) ? v : SettingDefaults.Values[key], CultureInfo.InvariantCulture);
}
