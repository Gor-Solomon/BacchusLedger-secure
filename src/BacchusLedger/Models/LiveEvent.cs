namespace BacchusLedger.Models;

/// One SIMULATED (or SENSOR) live event, stored only in live_events.
public sealed class LiveEvent
{
    public long Id { get; set; }
    public string CreatedAt { get; set; } = "";
    public string SimTime { get; set; } = "";
    public long? VesselId { get; set; }
    public string? VesselCode { get; set; }
    public string? VesselName { get; set; }
    public string Kind { get; set; } = "reading"; // reading | notification
    public string? Metric { get; set; }           // temperature | level | density
    public double? Value { get; set; }
    public string? Unit { get; set; }
    public string? Level { get; set; }            // red | amber | green (notifications)
    public string? Rule { get; set; }             // temp_high | level_drop | stuck | missing_brix | back_normal | all_good
    public string? Message { get; set; }
    public double? ValueUsd { get; set; }
    public string? Scenario { get; set; }
    public string Source { get; set; } = "SIMULATED";
    public bool Acknowledged { get; set; }
}
