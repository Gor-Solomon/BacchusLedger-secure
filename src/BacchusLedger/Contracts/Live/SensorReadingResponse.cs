namespace BacchusLedger.Contracts.Live;

public sealed record SensorReadingResponse(string Vessel, double DipCm, double Litres, string Source);
