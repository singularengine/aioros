// Minimal HTTP load generator with two modes.
// usage: LoadGen <url> <concurrency> <seconds> <serverRootPid> [statsUrl]
// Prints one JSON line: rps, latency percentiles (ms), errors, peak RSS of the server process tree (MB).
//
// Closed loop (default): <concurrency> workers each send a request, wait for its response, then send the next one. It
// measures the maximum throughput, but its latencies suffer coordinated omission: while the server stalls the workers
// stop sending, so the requests that would have waited are never measured (Gil Tene, "How NOT to Measure Latency").
//
// Open loop (LOADGEN_RATE=<req/s>): request i is scheduled at start + i/rate, whatever the server does, over at most
// <concurrency> connections. Its latency is measured from that intended send time, not from when it actually left, so
// a stall is charged to every request scheduled during it, as wrk2 does. scheduleLagMs reports how far the generator
// itself fell behind its schedule; when it is more than a few milliseconds the client, not the server, is the limit.
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

var url = args[0];
var concurrency = int.Parse(args[1]);
var seconds = double.Parse(args[2], CultureInfo.InvariantCulture);
var pid = int.Parse(args[3]);
var statsUrl = args.Length > 4 ? args[4] : null;
var rate = double.TryParse(Environment.GetEnvironmentVariable("LOADGEN_RATE"), CultureInfo.InvariantCulture, out var r) ? r : 0;

// LOADGEN_FORM="f0=a&_btn=0%7C" posts that form (with the page's antiforgery token) instead of getting the page.
var form = Environment.GetEnvironmentVariable("LOADGEN_FORM");
using var http = new HttpClient(new SocketsHttpHandler
{
    MaxConnectionsPerServer = concurrency,
    AutomaticDecompression = System.Net.DecompressionMethods.None,
    PooledConnectionLifetime = TimeSpan.FromMinutes(10),
    AllowAutoRedirect = false,
    UseCookies = form != null,
});
// LOADGEN_ENCODING="br, gzip" sends that Accept-Encoding (the compressed body is read, not decompressed). Unset, none
// is sent and every stack answers uncompressed.
if (Environment.GetEnvironmentVariable("LOADGEN_ENCODING") is { Length: > 0 } encoding)
{
    http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", encoding);
}
// LOADGEN_COOKIE=".AspNetCore.Identity.Application=..." sends that cookie on every request (a signed-in user).
if (Environment.GetEnvironmentVariable("LOADGEN_COOKIE") is { Length: > 0 } cookie && form == null)
{
    http.DefaultRequestHeaders.Add("Cookie", cookie);
}

string? body = null;
if (form != null)
{
    var page = await http.GetStringAsync(url);
    var token = System.Text.RegularExpressions.Regex.Match(page, "__RequestVerificationToken\" value=\"([^\"]+)").Groups[1].Value;
    body = form + "&__RequestVerificationToken=" + Uri.EscapeDataString(token);
}
Task<HttpResponseMessage> Send() => body == null
    ? http.GetAsync(url)
    : http.PostAsync(url, new StringContent(body, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"));

// Warm-up (LOADGEN_WARMUP seconds, default 3): JIT, caches and connections on both sides.
var warmup = double.TryParse(Environment.GetEnvironmentVariable("LOADGEN_WARMUP"), CultureInfo.InvariantCulture, out var w) ? w : 3;
var warmEnd = Stopwatch.StartNew();
await Task.WhenAll(Enumerable.Range(0, concurrency).Select(async _ =>
{
    while (warmEnd.Elapsed.TotalSeconds < warmup) (await Send()).Dispose();
}));

// Priming, always (even with LOADGEN_WARMUP=0): a few requests on every connection, so the open loop does not start by
// opening connections and compiling this process's own code. Without it a 2 s open loop started its schedule 70 to 110
// ms late and every stack's p99 measured that lag (CI run 80: 80 to 160 ms at 50 and 90% of capacity, p50 about 1 ms).
await Task.WhenAll(Enumerable.Range(0, concurrency).Select(async _ =>
{
    for (var primed = 0; primed < 8; primed++) (await Send()).Dispose();
}));

long peakRss = 0;
using var cts = new CancellationTokenSource();
var sampler = Task.Run(async () =>
{
    while (!cts.IsCancellationRequested)
    {
        peakRss = Math.Max(peakRss, TreeRss(pid));
        await Task.Delay(100);
    }
});

async Task<long?> Allocated() => statsUrl == null ? null
    : JsonDocument.Parse(await http.GetStringAsync(statsUrl)).RootElement.GetProperty("allocatedBytes").GetInt64();
var allocBefore = await Allocated();

var errors = 0;
async Task<long> Exchange()
{
    try
    {
        using var resp = await Send();
        await resp.Content.LoadIntoBufferAsync();
        if ((int)resp.StatusCode >= 400) Interlocked.Increment(ref errors);
    }
    catch { Interlocked.Increment(ref errors); }
    return Stopwatch.GetTimestamp();
}

var clock = Stopwatch.StartNew();
double[] all;
double lagMs = 0;
if (rate > 0)
{
    var total = (long)(rate * seconds);
    var measured = new double[total];
    var pending = new Task[total];
    var start = Stopwatch.GetTimestamp();
    var ticksPerRequest = Stopwatch.Frequency / rate;
    long next = 0;
    using var tick = new PeriodicTimer(TimeSpan.FromMilliseconds(1));
    while (next < total)
    {
        var now = Stopwatch.GetTimestamp() - start;
        lagMs = Math.Max(lagMs, (now - next * ticksPerRequest) * 1000.0 / Stopwatch.Frequency);
        var due = Math.Min(total, (long)(now / ticksPerRequest) + 1);
        for (; next < due; next++)
        {
            pending[next] = Scheduled(next, start + (long)(next * ticksPerRequest));
        }
        await tick.WaitForNextTickAsync();
    }
    await Task.WhenAll(pending);
    all = measured;

    async Task Scheduled(long i, long intended)
    {
        await Task.Yield();
        measured[i] = (await Exchange() - intended) * 1000.0 / Stopwatch.Frequency;
    }
}
else
{
    var latencies = new List<double>[concurrency];
    await Task.WhenAll(Enumerable.Range(0, concurrency).Select(async w =>
    {
        var mine = latencies[w] = new List<double>(100_000);
        while (clock.Elapsed.TotalSeconds < seconds)
        {
            var t = Stopwatch.GetTimestamp();
            mine.Add((await Exchange() - t) * 1000.0 / Stopwatch.Frequency);
        }
    }));
    all = latencies.SelectMany(l => l).ToArray();
}
var elapsed = clock.Elapsed.TotalSeconds;
var allocAfter = await Allocated();
cts.Cancel();
await sampler;

// Every latency is kept and sorted: exact percentiles, no histogram bucketing error.
Array.Sort(all);
double P(double q) => all[Math.Min(all.Length - 1, (int)(q * all.Length))];
Console.WriteLine(JsonSerializer.Serialize(new
{
    mode = rate > 0 ? "open" : "closed",
    concurrency,
    targetRps = rate > 0 ? rate : (double?)null,
    requests = all.Length,
    rps = Math.Round(all.Length / elapsed, 1),
    p50 = Math.Round(P(0.50), 2),
    p90 = Math.Round(P(0.90), 2),
    p95 = Math.Round(P(0.95), 2),
    p99 = Math.Round(P(0.99), 2),
    p999 = Math.Round(P(0.999), 2),
    max = Math.Round(all[^1], 2),
    errors,
    scheduleLagMs = rate > 0 ? Math.Round(lagMs, 2) : (double?)null,
    peakRssMb = Math.Round(peakRss / 1048576.0, 1),
    allocKbPerReq = allocBefore is { } b && allocAfter is { } a ? Math.Round((a - b) / 1024.0 / all.Length, 1) : (double?)null,
}));

static long TreeRss(int root)
{
    long total = 0;
    var stack = new Stack<int>([root]);
    while (stack.Count > 0)
    {
        var p = stack.Pop();
        try
        {
            var kb = File.ReadLines($"/proc/{p}/status").First(l => l.StartsWith("VmRSS:"))
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];
            total += long.Parse(kb) * 1024;
            foreach (var task in Directory.GetDirectories($"/proc/{p}/task"))
                foreach (var c in File.ReadAllText($"{task}/children").Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    stack.Push(int.Parse(c));
        }
        catch { /* process exited */ }
    }
    return total;
}
