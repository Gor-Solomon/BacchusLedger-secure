namespace BacchusLedger.Contracts.Report;

/// Simulated live data of one vessel: never part of the ledger, the balance or any journal.
public sealed record SimulatedSectionDto(string Vessel, string Source, string Note, IReadOnlyList<SimulatedAlertDto> Alerts, long Readings);
