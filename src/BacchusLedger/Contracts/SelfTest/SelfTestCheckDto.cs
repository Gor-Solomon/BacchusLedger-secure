namespace BacchusLedger.Contracts.SelfTest;

public sealed record SelfTestCheckDto(string Group, string Name, string Expected, string Actual, bool Pass);
