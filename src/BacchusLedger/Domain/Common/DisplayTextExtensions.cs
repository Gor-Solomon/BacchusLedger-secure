namespace BacchusLedger.Domain.Common;

/// How dates and vessels are written for people.
public static class DisplayTextExtensions
{
    /// "2025-09-18" → "18.09.2025".
    public static string ToDisplayDate(this string iso) => iso.Length >= 10 ? $"{iso[8..10]}.{iso[5..7]}.{iso[..4]}" : iso;

    /// "2025-09-18T09:00" → "18.09.2025 09:00".
    public static string ToDisplayDateTime(this string iso) => iso.Length >= 16 ? $"{iso.ToDisplayDate()} {iso[11..16]}" : iso.ToDisplayDate();

    /// Cellar label (what is written on the tape) with the internal code, e.g. "F.2 (F-1)"; the code alone when there is no label.
    public static string WithCellarLabel(this string? code, string? label) => code == null ? "" : string.IsNullOrWhiteSpace(label) ? code : $"{label} ({code})";
}
