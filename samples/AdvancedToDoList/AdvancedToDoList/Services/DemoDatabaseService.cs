using System.Collections.Immutable;
using Microsoft.Data.Sqlite;

namespace AdvancedToDoList.Services;

/// <summary>
/// The website's database: one in-memory SQLite database per visitor, seeded with the sample data, so each visitor of
/// the shared server has a to-do list of their own for as long as the server runs. A visitor is a cookie set on the
/// first page; the installed app working against the server sends none and shares one list.
/// </summary>
public sealed class DemoDatabaseService(IHttpContextAccessor http, DemoDatabases databases) : IDatabaseService
{
    public const string VisitorCookie = "todo-visitor";

    public string GetDatabasePath() => databases.Open(http.HttpContext?.Items[VisitorCookie] as string ?? "installed-app");

    public Task SaveAsync() => Task.CompletedTask;

    /// <summary>Gives each browser a visitor id: the cookie it sent, or a new one set on a page (never on the installed app's calls).</summary>
    public static async Task IdentifyVisitorAsync(HttpContext context, Func<Task> next)
    {
        if (context.Request.Cookies[VisitorCookie] is { Length: 32 } visitor && visitor.All(char.IsAsciiHexDigitLower))
        {
            context.Items[VisitorCookie] = visitor;
        }
        else if (!context.Request.Path.StartsWithSegments("/_sf") && !context.Request.Path.StartsWithSegments("/_data"))
        {
            var created = Guid.NewGuid().ToString("N");
            context.Items[VisitorCookie] = created;
            context.Response.Cookies.Append(VisitorCookie, created, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, IsEssential = true });
        }

        await next();
    }
}

/// <summary>The visitors' in-memory databases, each kept alive by one open connection; the oldest go first past a limit.</summary>
public sealed class DemoDatabases : IDisposable
{
    private const int Kept = 256;

    private ImmutableDictionary<string, SqliteConnection> _open = ImmutableDictionary<string, SqliteConnection>.Empty;
    private ImmutableQueue<string> _order = ImmutableQueue<string>.Empty;

    public string Open(string visitor)
    {
        var path = $"file:todo-{visitor}?mode=memory&cache=shared";
        if (!_open.ContainsKey(visitor))
        {
            var keeper = new SqliteConnection($"Data Source='{path}'");
            keeper.Open();
            if (ImmutableInterlocked.TryAdd(ref _open, visitor, keeper))
            {
                ImmutableInterlocked.Enqueue(ref _order, visitor);
                while (_open.Count > Kept && ImmutableInterlocked.TryDequeue(ref _order, out var oldest)
                       && ImmutableInterlocked.TryRemove(ref _open, oldest, out var closed))
                {
                    closed.Dispose();
                }
            }
            else
            {
                keeper.Dispose();
            }
        }

        return path;
    }

    public void Dispose()
    {
        foreach (var connection in _open.Values)
        {
            connection.Dispose();
        }
    }
}
