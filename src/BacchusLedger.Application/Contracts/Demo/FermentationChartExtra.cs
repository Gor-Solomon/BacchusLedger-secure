namespace BacchusLedger.Application.Contracts.Demo;

public sealed record FermentationChartExtra(string Chart, IReadOnlyList<int> Days, IReadOnlyList<string> Dates, double[] Sugar, double[] Temp, string Source);
