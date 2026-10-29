namespace BacchusLedger.Dataset;

public sealed record SeedBatch(string Code, string Winery, string Date, double Kg, string WineType, string Tech, string Variety, double? Abv);
