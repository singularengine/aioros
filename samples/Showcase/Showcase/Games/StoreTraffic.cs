using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Showcase.Games;

public sealed record TrafficEvent(string Kind, double Milliseconds, long Bytes, bool Failed, string? Player = null, int GameId = 0);

public sealed record FeedItem(string Text, string Detail, bool Buy);

public sealed record TopSeller(string Title, int Sales, string Art);

public sealed record TrafficPoint(DateTime At, double Value);

public sealed record TrafficSnapshot(
    int Players,
    double PagesPerSecond,
    double PostsPerSecond,
    double P50,
    double P99,
    double MegabytesPerSecond,
    long Total,
    long Errors,
    int Threads,
    ImmutableArray<TrafficPoint> Throughput,
    ImmutableArray<TrafficPoint> Latency,
    ImmutableArray<FeedItem> Feed,
    ImmutableArray<TopSeller> Top)
{
    public static TrafficSnapshot Idle { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, [], [], [], []);
}

/// <summary>
/// Virtual players that use the store the way a browser does, over HTTP: each signed in with its own ASP.NET Core
/// Identity account, they open XAML pages (the store with random filters, games, their library) and buy through the
/// page's form post. A control loop owns the crew, a metrics loop owns the numbers; both are
/// fed by channels.
/// </summary>
public sealed partial class StoreTraffic(IServiceProvider services, TimeProvider time) : BackgroundService
{
    private static readonly string[] Sorts = ["top", "rated", "new", "price"];
    private readonly Channel<int> _crew = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = true });
    private readonly TrafficMeter _meter = new(time);
    private int _players;

    public TrafficSnapshot Snapshot => _meter.Snapshot;

    public void SetPlayers(int players) => _crew.Writer.TryWrite(Math.Max(0, players));

    /// <summary>Notes the server thread that handled a page request, so the lab can show how many threads served pages.</summary>
    public void Served() => _meter.Served();

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(CrewAsync(stoppingToken), _meter.RunAsync(() => Volatile.Read(ref _players), stoppingToken));

    private async Task CrewAsync(CancellationToken stoppingToken)
    {
        var crew = new List<CancellationTokenSource>();
        ImmutableArray<string> sessions = [];
        using var handler = new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false, MaxConnectionsPerServer = 4096, PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5) };
        using var http = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(60) };
        try
        {
            await foreach (var size in _crew.Reader.ReadAllAsync(stoppingToken))
            {
                if (size > 0 && sessions.IsEmpty)
                {
                    http.BaseAddress = PlayerSessions.Address(services);
                    sessions = await PlayerSessions.CreateAsync(services);
                }

                while (crew.Count > size)
                {
                    await crew[^1].CancelAsync();
                    crew[^1].Dispose();
                    crew.RemoveAt(crew.Count - 1);
                }

                while (crew.Count < size)
                {
                    var stop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    var index = crew.Count;
                    crew.Add(stop);
                    var session = sessions[index % sessions.Length];
                    _ = Task.Run(() => PlayAsync(http, index, session, stop.Token), CancellationToken.None);
                }

                Volatile.Write(ref _players, crew.Count);
            }
        }
        finally
        {
            foreach (var stop in crew)
            {
                await stop.CancelAsync();
                stop.Dispose();
            }
        }
    }

    private async Task PlayAsync(HttpClient http, int index, string session, CancellationToken stop)
    {
        var random = new Random(index * 7 + 1);
        var player = GameSeeder.Account(1 + (index % GameSeeder.Accounts));
        var antiforgery = string.Empty;
        while (!stop.IsCancellationRequested)
        {
            var roll = random.Next(100);
            try
            {
                if (roll < 45)
                {
                    var genre = GameCatalog.Genres[random.Next(GameCatalog.Genres.Length)];
                    await PageAsync(http, $"/games?genre={genre}&sort={Sorts[random.Next(Sorts.Length)]}&page={1 + random.Next(5)}", "page", session, antiforgery, stop);
                }
                else if (roll < 75)
                {
                    await PageAsync(http, "/games/" + Popular(random).ToString(CultureInfo.InvariantCulture), "page", session, antiforgery, stop);
                }
                else if (roll < 80)
                {
                    await PageAsync(http, "/games/library", "page", session, antiforgery, stop);
                }
                else
                {
                    antiforgery = await BuyAsync(http, Popular(random), player, session, antiforgery, stop);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                return;
            }
            catch (Exception) when (!stop.IsCancellationRequested)
            {
                _meter.Record(new TrafficEvent("page", 0, 0, true));
            }
        }
    }

    private async Task<(string Html, string Antiforgery)> PageAsync(HttpClient http, string path, string kind, string session, string antiforgery, CancellationToken stop)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", Cookies(session, antiforgery));
        request.Headers.Add("Accept-Encoding", "br");
        var clock = Stopwatch.StartNew();
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, stop);
        var body = await response.Content.ReadAsByteArrayAsync(stop);
        _meter.Record(new TrafficEvent(kind, clock.Elapsed.TotalMilliseconds, body.Length, response.StatusCode != HttpStatusCode.OK));
        var html = response.Content.Headers.ContentEncoding.Count == 0 ? System.Text.Encoding.UTF8.GetString(body) : string.Empty;
        return (html, Antiforgery(response) ?? antiforgery);
    }

    private async Task<string> BuyAsync(HttpClient http, int game, string player, string session, string antiforgery, CancellationToken stop)
    {
        using var get = new HttpRequestMessage(HttpMethod.Get, "/games/" + game.ToString(CultureInfo.InvariantCulture));
        get.Headers.Add("Cookie", Cookies(session, antiforgery));
        var clock = Stopwatch.StartNew();
        using var page = await http.SendAsync(get, stop);
        var html = await page.Content.ReadAsStringAsync(stop);
        _meter.Record(new TrafficEvent("page", clock.Elapsed.TotalMilliseconds, html.Length, page.StatusCode != HttpStatusCode.OK));
        antiforgery = Antiforgery(page) ?? antiforgery;
        if (TokenPattern().Match(html) is not { Success: true } token || !html.Contains("value=\"1|\"", StringComparison.Ordinal))
        {
            return antiforgery;
        }

        using var post = new HttpRequestMessage(HttpMethod.Post, "/games/" + game.ToString(CultureInfo.InvariantCulture))
        {
            Content = new FormUrlEncodedContent([new("__RequestVerificationToken", WebUtility.HtmlDecode(token.Groups[1].Value)), new("_btn", "1|")]),
        };
        post.Headers.Add("Cookie", Cookies(session, antiforgery));
        clock.Restart();
        using var bought = await http.SendAsync(post, stop);
        _meter.Record(new TrafficEvent("post", clock.Elapsed.TotalMilliseconds, 0, bought.StatusCode != HttpStatusCode.SeeOther, player, game));
        return antiforgery;
    }

    private static string Cookies(string session, string antiforgery) => antiforgery.Length == 0 ? session : session + "; " + antiforgery;

    private static string? Antiforgery(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Where(cookie => cookie.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal)).Select(cookie => cookie.Split(';')[0]).FirstOrDefault()
            : null;

    private static int Popular(Random random) => 1 + (int)(Math.Pow(random.NextDouble(), 2.2) * (GameSeeder.GameCount - 1));

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();

}
