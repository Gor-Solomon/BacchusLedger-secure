using System.Globalization;
using System.Text.RegularExpressions;

namespace BacchusLedger.Domain.Assistant;

/// Checks on an AI answer: every number must come from a tool result; source tags are collected.
public static class AnswerTextExtensions
{
    static readonly string[] KnownTags = ["Dataset", "Calculated", "DEMO ASSUMPTION", "SIMULATED", "SENSOR", "Entered", "Challenge Book", "Kakheti PDO spec", "CBW 2025", "Hackathon slides", "TBC Capital 2024"];
    static readonly Regex Codes = new(@"\[[^\]]*\]|\b\d{1,4}[./-]\d{1,2}[./-]\d{2,4}\b|\b\d{1,2}:\d{2}\b|\b[A-Za-z]+[A-Za-z0-9]*(?:[-.][A-Za-z0-9]+)+\b");
    static readonly Regex Num = new(@"\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?");

    /// Known source tags written in [brackets] in the answer.
    public static List<string> SourceTags(this string text) =>
        Regex.Matches(text, @"\[([^\[\]]{2,60})\]").SelectMany(m => m.Groups[1].Value.Split(',', ';')).Select(t => t.Trim()).Where(t => KnownTags.Any(k => t.StartsWith(k))).Distinct().ToList();

    /// True when every number in the answer (codes, dates and small counts aside) matches a number in the tool results (rounding allowed).
    public static bool UsesOnlyNumbersFrom(this string answer, IEnumerable<string> toolOutputs)
    {
        var known = toolOutputs.SelectMany(Numbers).ToHashSet();
        foreach (var a in Numbers(Codes.Replace(answer, " ")))
        {
            if (a <= 12 && a == Math.Floor(a)) continue; // counts, list positions, "2nd"
            if (!known.Any(b => Math.Abs(a - b) <= Math.Max(0.051, 0.005 * b))) return false;
        }
        return true;
    }

    static IEnumerable<double> Numbers(string s) => Num.Matches(s).Select(m => double.Parse(m.Value.Replace(",", ""), CultureInfo.InvariantCulture));
}
