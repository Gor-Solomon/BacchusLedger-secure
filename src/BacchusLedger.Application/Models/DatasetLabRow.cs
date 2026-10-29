namespace BacchusLedger.Application.Models;

/// A dataset lab row (to derive readings for databases created before readings existed).
public sealed record DatasetLabRow(long BatchId, long? VesselId, string Date, string Source, string? Stage, string? Note, double? TempC, double? Ph, double? TaGl);
