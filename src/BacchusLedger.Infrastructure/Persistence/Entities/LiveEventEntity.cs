namespace BacchusLedger.Infrastructure.Persistence.Entities;

/// Table live_events: A SIMULATED (or SENSOR) live event. Never read by journals, the loss calculator or ROI numbers.
public sealed class LiveEventEntity
{
    public long Id { get; set; }
    public string? CreatedAt { get; set; }
    public string? SimTime { get; set; }
    public long? VesselId { get; set; }
    public string? VesselCode { get; set; }
    public string? VesselName { get; set; }
    public string? Kind { get; set; }
    public string? Metric { get; set; }
    public double? Value { get; set; }
    public string? Unit { get; set; }
    public string? Level { get; set; }
    public string? Rule { get; set; }
    public string? Message { get; set; }
    public double? ValueUsd { get; set; }
    public string? Scenario { get; set; }
    public string Source { get; set; } = "";
    public long? Acknowledged { get; set; }
}
