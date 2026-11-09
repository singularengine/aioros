using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;

namespace Showcase.Games;

/// <summary>Turns what the virtual players report into the lab's numbers. One loop owns them; readers get snapshots.</summary>
internal sealed class TrafficMeter(TimeProvider time)
{
    private readonly Channel<TrafficEvent> _events = Channel.CreateBounded<TrafficEvent>(new BoundedChannelOptions(100_000) { SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Channel<int> _threads = Channel.CreateBounded<int>(new BoundedChannelOptions(100_000) { SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest });
    private TrafficSnapshot _snapshot = TrafficSnapshot.Idle;

    public TrafficSnapshot Snapshot => Volatile.Read(ref _snapshot);

    public void Record(TrafficEvent entry) => _events.Writer.TryWrite(entry);

    public void Served() => _threads.Writer.TryWrite(Environment.CurrentManagedThreadId);

    public async Task RunAsync(Func<int> players, CancellationToken stoppingToken)
    {
        var window = new List<TrafficEvent>();
        var threads = new HashSet<int>();
        var recent = new Queue<(DateTime At, int Game)>();
        var throughput = ImmutableArray<TrafficPoint>.Empty;
        var latency = ImmutableArray<TrafficPoint>.Empty;
        var feed = ImmutableArray<FeedItem>.Empty;
        var titles = new Dictionary<int, string>();
        long total = 0;
        long errors = 0;
        var clock = Stopwatch.StartNew();
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var seconds = Math.Max(0.001, clock.Elapsed.TotalSeconds);
            clock.Restart();
            window.Clear();
            threads.Clear();
            while (_events.Reader.TryRead(out var entry))
            {
                window.Add(entry);
            }

            while (_threads.Reader.TryRead(out var thread))
            {
                threads.Add(thread);
            }

            var now = time.GetUtcNow().UtcDateTime;
            total += window.Count;
            errors += window.Count(entry => entry.Failed);
            foreach (var entry in window.Where(entry => entry.Kind == "post" && !entry.Failed))
            {
                recent.Enqueue((now, entry.GameId));
                if (!titles.ContainsKey(entry.GameId))
                {
                    titles[entry.GameId] = GameCatalog.Game(entry.GameId).Title;
                }
            }

            while (recent.Count > 0 && now - recent.Peek().At > TimeSpan.FromSeconds(10))
            {
                recent.Dequeue();
            }

            var pages = window.Count(entry => entry.Kind == "page") / seconds;
            var posts = window.Count(entry => entry.Kind == "post") / seconds;
            var sorted = window.Where(entry => !entry.Failed).Select(entry => entry.Milliseconds).Order().ToArray();
            var p99 = Percentile(sorted, 0.99);
            throughput = Append(throughput, new TrafficPoint(now, pages + posts));
            latency = Append(latency, new TrafficPoint(now, p99));
            feed = [.. window.Where(entry => entry.Kind == "post" && !entry.Failed && entry.Player is not null).TakeLast(6).Reverse()
                .Select(entry => new FeedItem($"{entry.Player} bought {titles[entry.GameId]}", string.Create(CultureInfo.InvariantCulture, $"form post · {entry.Milliseconds:0.0} ms"), true))
                .Concat(feed).Take(8)];
            var top = recent.GroupBy(entry => entry.Game).OrderByDescending(group => group.Count()).Take(5)
                .Select(group => new TopSeller(titles[group.Key], group.Count(), GameArt.Url(group.Key, "cover"))).ToImmutableArray();
            Volatile.Write(ref _snapshot, new TrafficSnapshot(players(), pages, posts, Percentile(sorted, 0.5), p99,
                window.Sum(entry => entry.Bytes) / seconds / 1_048_576, total, errors, threads.Count, throughput, latency, feed, top));
        }
    }

    private static ImmutableArray<TrafficPoint> Append(ImmutableArray<TrafficPoint> points, TrafficPoint point) =>
        points.Length >= 120 ? points.RemoveAt(0).Add(point) : points.Add(point);

    private static double Percentile(double[] sorted, double share) =>
        sorted.Length == 0 ? 0 : sorted[Math.Min(sorted.Length - 1, (int)(share * sorted.Length))];
}
