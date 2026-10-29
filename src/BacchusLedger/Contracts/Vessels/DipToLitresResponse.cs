namespace BacchusLedger.Contracts.Vessels;

public sealed record DipToLitresResponse(string Vessel, double DipCm, double FillCm, double Litres, double CapacityL, string Source);
