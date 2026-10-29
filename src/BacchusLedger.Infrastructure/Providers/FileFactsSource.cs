using System.Text.RegularExpressions;
using BacchusLedger.Application.Providers;

namespace BacchusLedger.Infrastructure.Providers;

/// Reads knowledge/facts.md next to the app once: every line starting with "- " is a fact.
public sealed class FileFactsSource : IFactsSource
{
    readonly Lazy<List<string>> lines = new(() =>
    {
        var p = Path.Combine(AppContext.BaseDirectory, "knowledge", "facts.md");
        return File.Exists(p) ? File.ReadAllLines(p).Where(l => l.StartsWith("- ")).Select(l => l[2..].Trim()).ToList() : [];
    });

    public List<string> Search(string query, int max = 6)
    {
        var words = Regex.Split(query.ToLowerInvariant(), @"[^\p{L}\p{N}]+").Where(w => w.Length >= 3).Select(w => w.Length > 6 ? w[..6] : w).Distinct().ToList();
        if (words.Count == 0) return lines.Value.Take(max).ToList();
        return lines.Value.Select(f => (f, score: words.Count(w => f.Contains(w, StringComparison.OrdinalIgnoreCase))))
            .Where(x => x.score > 0).OrderByDescending(x => x.score).Take(max).Select(x => x.f).ToList();
    }
}
