namespace BacchusLedger.Application.Contracts.Readings;

/// One checked (preview) or saved reading; Id is null in a preview.
public sealed record ReadingResultDto(long? Id, string Type, double Value, string Unit, ReadingCalcDto Calc, IReadOnlyList<string> Warnings, string Source);
