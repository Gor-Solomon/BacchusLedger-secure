namespace BacchusLedger.Application.Contracts.Vessels;

/// Litres of one batch in a vessel now.
public sealed record VesselContentDto(string? Batch, long BatchId, double Litres, bool Copy);
