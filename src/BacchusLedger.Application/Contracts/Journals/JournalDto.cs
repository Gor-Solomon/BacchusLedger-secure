namespace BacchusLedger.Application.Contracts.Journals;

/// One Kakheti PDO journal. Rows are keyed by column key (plus "source"); the column set differs per journal,
/// so a row is a key → value map rather than a fixed record.
public sealed record JournalDto(int N, string Title, string Note, IReadOnlyList<JournalColumnDto> Columns, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);
