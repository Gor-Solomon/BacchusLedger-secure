namespace BacchusLedger.Contracts.Demo;

public sealed record DemoStepDto(int N, string Title, string? Date, int[] Journals, bool Written);
