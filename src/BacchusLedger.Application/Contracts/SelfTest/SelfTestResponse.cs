namespace BacchusLedger.Application.Contracts.SelfTest;

public sealed record SelfTestResponse(bool Pass, int Passed, int Total, string Note, IReadOnlyList<SelfTestCheckDto> Checks);
