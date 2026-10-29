namespace BacchusLedger.Contracts.Journals;

/// CSV export of a journal (UTF-8 with BOM so Excel opens Georgian text).
public sealed record JournalCsvFile(byte[] Content, string FileName);
