namespace BacchusLedger.Contracts.Demo;

public sealed record LiveDemoStartResponse(long BatchId, string BatchCode, string Vessel, string Winery);
