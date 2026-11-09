using Npgsql;

namespace ViewScale.Data;

public sealed record Row(int Id, string Title, string Status, DateTime Updated)
{
    public string UpdatedText => Updated.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}

public interface IRowSource
{
    ValueTask<Row[]> PageAsync(int offset, CancellationToken cancellationToken);
}

public static class RowSet
{
    public const int Total = 1000;
    public const int PageSize = 10;
}

public sealed class MemoryRows : IRowSource
{
    private static readonly Row[] Rows = [.. Enumerable.Range(1, RowSet.Total)
        .Select(i => new Row(i, $"Item {i}", (i % 4) switch { 0 => "todo", 1 => "progress", 2 => "review", _ => "done" },
            new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(-i)))];

    public ValueTask<Row[]> PageAsync(int offset, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Rows[offset..(offset + RowSet.PageSize)]);
}

public sealed class PostgresRows(NpgsqlDataSource dataSource) : IRowSource
{
    public async ValueTask<Row[]> PageAsync(int offset, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT id, title, status, updated FROM items ORDER BY updated DESC LIMIT 10 OFFSET $1");
        command.Parameters.Add(new NpgsqlParameter { Value = offset });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<Row>(RowSet.PageSize);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new Row(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDateTime(3)));
        }
        return [.. rows];
    }
}
