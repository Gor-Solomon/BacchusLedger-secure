namespace BacchusLedger.Contracts.Operations;

/// How a dip reading was turned into litres for an operation.
public sealed record DipInfoDto(string DipVessel, double? DipCm, double LitresInVessel, double AlreadyInVessel, string? Source);
