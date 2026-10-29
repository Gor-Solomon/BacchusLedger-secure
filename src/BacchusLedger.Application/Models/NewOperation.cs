namespace BacchusLedger.Application.Models;

/// A cellar operation to append (operations are append-only).
public sealed record NewOperation(
    long BatchId, string Date, string Type, long? FromVesselId, long? ToVesselId, double? Litres, int? Bottles, double? BottleMl,
    double? RemainderL, string? Method, double? DipCm, string? Person, string? Note, string Source, string CreatedAt);
