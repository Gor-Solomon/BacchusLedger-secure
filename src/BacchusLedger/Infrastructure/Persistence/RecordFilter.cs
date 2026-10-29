namespace BacchusLedger.Infrastructure.Persistence;

/// Optional batch id / winery code filter as SQL conditions with positional parameters.
public sealed record RecordFilter(long? Batch, string? Winery)
{
    /// Batches (alias.id, alias.winery_id).
    public (string Sql, object?[] Args) ForBatches(string alias) => Build(
        $"{alias}.id=$#",
        $"{alias}.winery_id=(SELECT id FROM wineries WHERE code=$#)");

    /// Plots that delivered to the batch / belong to the winery.
    public (string Sql, object?[] Args) ForPlots() => Build(
        "p.id IN (SELECT d.plot_id FROM batch_origins bo JOIN deliveries d ON d.id=bo.delivery_id WHERE bo.batch_id=$#)",
        "p.winery_id=(SELECT id FROM wineries WHERE code=$#)");

    /// Deliveries of the batch / from plots of the winery.
    public (string Sql, object?[] Args) ForDeliveries() => Build(
        "d.id IN (SELECT delivery_id FROM batch_origins WHERE batch_id=$#)",
        "p.winery_id=(SELECT id FROM wineries WHERE code=$#)");

    (string, object?[]) Build(string batchCondition, string wineryCondition)
    {
        var parts = new List<string>(); var args = new List<object?>();
        if (Batch != null) { args.Add(Batch); parts.Add(batchCondition.Replace("$#", "$" + args.Count)); }
        if (!string.IsNullOrEmpty(Winery)) { args.Add(Winery); parts.Add(wineryCondition.Replace("$#", "$" + args.Count)); }
        return (parts.Count == 0 ? "1=1" : string.Join(" AND ", parts), args.ToArray());
    }
}
