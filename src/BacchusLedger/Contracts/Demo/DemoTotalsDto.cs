namespace BacchusLedger.Contracts.Demo;

public sealed record DemoTotalsDto(double KgReceived, double LitresNow, double BottledL, double TotalLossL, double LossValueUsd, int JournalsFilled, int JournalsTotal);
