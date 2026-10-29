using System.Globalization;
using Microsoft.Data.Sqlite;

namespace BacchusLedger.Infrastructure.Persistence;

/// Typed access to the current row of a reader, by column name.
public sealed class SqliteRow(SqliteDataReader reader)
{
    public object? Value(string column)
    {
        int i = reader.GetOrdinal(column);
        return reader.IsDBNull(i) ? null : reader.GetValue(i);
    }

    public string? Str(string column) => Value(column) as string;

    public long Long(string column) => LongOrNull(column) ?? 0;

    public long? LongOrNull(string column) => Value(column) is { } v ? Convert.ToInt64(v, CultureInfo.InvariantCulture) : null;

    public double? Dbl(string column) => Value(column) is { } v ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : null;

    public Dictionary<string, object?> ToDictionary()
    {
        var row = new Dictionary<string, object?>();
        for (int i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        return row;
    }
}
