namespace BacchusLedger.Application.Contracts.Origins;

public sealed record FarmerDto(long Id, string? Code, string? Name, string? Phone, long? IsDemo, long Plots, double? KgDelivered);
