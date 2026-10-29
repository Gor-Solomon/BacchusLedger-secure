using System.Text.Json.Serialization;

namespace BacchusLedger.Application.Contracts.Readings;

/// Calculated values of one reading; only the values that apply are written ({} when none).
public sealed record ReadingCalcDto(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? PotentialAbv,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Sg,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? ProgressPct,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Formula);
