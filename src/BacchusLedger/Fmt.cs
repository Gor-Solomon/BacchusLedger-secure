namespace BacchusLedger;

public static class Fmt
{
    public static string Date(string iso) => iso.Length >= 10 ? $"{iso[8..10]}.{iso[5..7]}.{iso[..4]}" : iso;
    public static string DateTime(string iso) => iso.Length >= 16 ? $"{Date(iso)} {iso[11..16]}" : Date(iso);
    /// Cellar label (what is written on the tape) with the internal code, e.g. "F.2 (F-1)".
    public static string Vessel(string? code, string? label) => code == null ? "" : string.IsNullOrWhiteSpace(label) ? code : $"{label} ({code})";
}
