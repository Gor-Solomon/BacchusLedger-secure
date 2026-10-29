namespace BacchusLedger.Contracts.Demo;

public sealed record DemoStepsDto(string Script, string BatchCode, int Total, IReadOnlyList<DemoStepDto> Steps);
