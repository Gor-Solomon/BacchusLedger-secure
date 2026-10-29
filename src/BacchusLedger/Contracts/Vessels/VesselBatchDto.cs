namespace BacchusLedger.Contracts.Vessels;

/// A batch that used the vessel, with the date it last touched it.
public sealed record VesselBatchDto(long Id, string? Code, string? Last);
