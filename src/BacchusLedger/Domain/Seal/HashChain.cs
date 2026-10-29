using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BacchusLedger.Domain.Seal;

/// Tamper evidence: every record entry of a batch is chained, hash = SHA-256(previous hash | table | row content).
/// Head is the hash of the last entry (the genesis hash of the batch code when the chain is empty).
public sealed class HashChain(string head)
{
    public string Head { get; private set; } = head;

    /// An empty chain for a batch.
    public static HashChain Start(string batchCode) => new(Sha("genesis|" + batchCode));

    /// Hashes the next entry, moves the head to it and returns the new hash.
    public string Append(string table, IReadOnlyDictionary<string, object?> row) => Head = Sha($"{Head}|{table}|{Content(row)}");

    /// Canonical row content: all columns, sorted by name (ordinal), as JSON.
    static string Content(IReadOnlyDictionary<string, object?> row)
    {
        var sorted = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (k, v) in row) sorted[k] = v;
        return JsonSerializer.Serialize(sorted);
    }

    static string Sha(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
}
