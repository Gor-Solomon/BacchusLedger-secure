namespace BacchusLedger.Models;

/// The first sealed lab row with a temperature (demo tamper target).
public sealed record SealedLabTemperature(long Seq, long LabId, string? Date, double TempC);
