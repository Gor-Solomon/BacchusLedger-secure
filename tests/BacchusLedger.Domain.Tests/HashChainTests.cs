using BacchusLedger.Domain.Seal;

namespace BacchusLedger.Domain.Tests;

public class HashChainTests
{
    static readonly Dictionary<string, object?>[] Rows =
    [
        new() { ["id"] = 1L, ["date"] = "2025-10-15", ["temp_c"] = 17.6, ["note"] = null },
        new() { ["temp_c"] = 15.2, ["id"] = 2L, ["date"] = "2025-11-20", ["note"] = "storage" },
    ];

    static List<string> Hashes(IEnumerable<Dictionary<string, object?>> rows)
    {
        var chain = HashChain.Start("W03-B3-2025");
        return rows.Select(r => chain.Append("lab", r)).ToList();
    }

    [Fact]
    public void Recomputing_the_same_rows_verifies() => Assert.Equal(Hashes(Rows), Hashes(Rows));

    [Fact]
    public void Column_order_does_not_matter()
    {
        var reordered = Rows.Select(r => r.Reverse().ToDictionary(kv => kv.Key, kv => kv.Value)).ToList();
        Assert.Equal(Hashes(Rows), Hashes(reordered));
    }

    [Fact]
    public void Changing_one_value_changes_that_entry_and_every_later_one()
    {
        var tampered = Rows.Select(r => new Dictionary<string, object?>(r)).ToArray();
        tampered[0]["temp_c"] = 19.1;
        var a = Hashes(Rows); var b = Hashes(tampered);
        Assert.NotEqual(a[0], b[0]);
        Assert.NotEqual(a[1], b[1]);
    }

    [Fact]
    public void A_chain_continues_from_its_head()
    {
        var full = Hashes(Rows);
        var resumed = new HashChain(full[0]).Append("lab", Rows[1]);
        Assert.Equal(full[1], resumed);
    }

    [Fact]
    public void Different_batches_have_different_genesis() =>
        Assert.NotEqual(HashChain.Start("W01-B1-2025").Head, HashChain.Start("W03-B3-2025").Head);
}
