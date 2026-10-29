namespace BacchusLedger.Application.Contracts.Vessels;

public sealed record LitresToDipResponse(string Vessel, double Litres, double DipCm, double CapacityL, string Source);
