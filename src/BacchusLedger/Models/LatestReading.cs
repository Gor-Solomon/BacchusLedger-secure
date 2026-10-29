namespace BacchusLedger.Models;

/// A reading with its vessel (assistant answers).
public sealed record LatestReading(string Type, double? Value, string? Unit, string TakenAt, string? Source, string? Vessel, string? CellarLabel);
