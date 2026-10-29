namespace BacchusLedger.Application.Services.Assistant;

public sealed record LossStep(string Stage, string Date, double LossL, double? LossPct, string Status);
