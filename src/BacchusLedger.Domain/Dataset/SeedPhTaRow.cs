namespace BacchusLedger.Domain.Dataset;

/// Dataset A4.1 row: fermentation day with pH and TA.
public sealed record SeedPhTaRow(string Date, int Day, double Ph, double Ta);
