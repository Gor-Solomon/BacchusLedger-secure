namespace BacchusLedger.Contracts.Report;

/// Report without a batch: only the simulated section of a vessel (Batch is always null).
public sealed record VesselReportResponse(ReportBatchDto? Batch, string? Vessel, SimulatedSectionDto? Simulated, string GeneratedAt);
