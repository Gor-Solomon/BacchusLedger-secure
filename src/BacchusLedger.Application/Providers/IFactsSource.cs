namespace BacchusLedger.Application.Providers;

/// knowledge/facts.md: one fact per line with its source tag.
public interface IFactsSource
{
    /// Keyword search (no embeddings): facts with the most matching words first.
    List<string> Search(string query, int max = 6);
}
