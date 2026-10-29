namespace BacchusLedger.Application.Contracts.Live;

/// State of the (SIMULATED) live board.
public sealed record LiveStateDto(
    bool On,
    string Scenario,
    string ScenarioTitle,
    string SimTime,
    LiveThresholdsDto Thresholds,
    IReadOnlyList<RangeAlertDto> RangeAlerts,
    IReadOnlyList<LiveVesselDto> Vessels,
    string Source);
