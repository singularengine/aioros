using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Aioros.Runtime.Caching;
using Aioros.Runtime.Pages;
using Aioros.Runtime.SignIn;
using Microsoft.Extensions.Time.Testing;

namespace Aioros.Runtime.Tests.Pages;

public sealed class LiveReconnectionTests
{
    private const string Opened = "event: open\ndata: \n\n";

    [Fact]
    [Trait("AC", "DESKTOP-77")]
    public void WaitsDoubleUpToTheCapWithFullJitter()
    {
        var half = new LiveReconnection(TimeProvider.System, static () => 0.5);
        var whole = new LiveReconnection(TimeProvider.System, static () => 1);

        Assert.Equal([0.5, 1, 2, 4, 8, 15, 15], [.. Enumerable.Range(0, 7).Select(attempt => half.Delay(attempt).TotalSeconds)]);
        Assert.Equal(30, whole.Delay(int.MaxValue).TotalSeconds);
        Assert.Equal([5, 10, 20, 30], [.. Enumerable.Range(0, 4).Select(attempt => whole.Delay(attempt, TimeSpan.FromSeconds(5)).TotalSeconds)]);
        Assert.Equal(60, whole.Delay(3, TimeSpan.FromSeconds(60)).TotalSeconds);
        Assert.Equal(1, whole.Delay(0, TimeSpan.Zero).TotalSeconds);
        Assert.Equal(TimeSpan.Zero, new LiveReconnection(TimeProvider.System, static () => 0).Delay(4));
    }

    [Fact(Timeout = 30_000)]
    [Trait("AC", "DESKTOP-78")]
    public async Task FailuresWaitLongerAndAReconnectRevalidatesEverythingAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new Clock();
        var server = new Script(Status(HttpStatusCode.ServiceUnavailable), Unreachable, Stream(Opened + "event: keys\ndata: sales\n\n"));
        await using var watch = Watch(server, Reconnection(clock), token);

        var first = watch.MoveNextAsync();
        var shortWait = await clock.Waits.Reader.ReadAsync(token);
        var beforeShort = server.Requests.Count;
        clock.Advance(shortWait);
        var longWait = await clock.Waits.Reader.ReadAsync(token);
        var beforeLong = server.Requests.Count;
        clock.Advance(longWait);
        Assert.True(await first);
        var everything = watch.Current;
        Assert.True(await watch.MoveNextAsync());

        Assert.Equal([0.5, 1], [shortWait.TotalSeconds, longWait.TotalSeconds]);
        Assert.Equal((1, 2), (beforeShort, beforeLong));
        Assert.Equal(new Invalidation(ImmutableHashSet<string>.Empty, true), everything);
        Assert.Equal(["sales"], watch.Current.Keys);
        Assert.False(watch.Current.All);
        Assert.Equal(["/_live?keys=sales none", "/_live?keys=sales none", "/_live?keys=sales none"], server.Requests);
    }

    [Fact(Timeout = 30_000)]
    [Trait("AC", "DESKTOP-79")]
    public async Task ASuccessfulReconnectStartsTheBackoffAgainAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new Clock();
        var server = new Script(Unreachable, Unreachable, Stream(Opened));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var watch = Watch(server, Reconnection(clock), stop.Token);

        var everything = watch.MoveNextAsync();
        clock.Advance(await clock.Waits.Reader.ReadAsync(token));
        clock.Advance(await clock.Waits.Reader.ReadAsync(token));
        Assert.True(await everything);
        var next = watch.MoveNextAsync();
        var wait = await clock.Waits.Reader.ReadAsync(token);
        var pending = !next.IsCompleted;
        await StopAsync(stop, watch, next);

        Assert.Equal(TimeSpan.FromSeconds(0.5), wait);
        Assert.Equal(3, server.Requests.Count);
        Assert.True(pending);
    }

    [Fact(Timeout = 30_000)]
    [Trait("AC", "DESKTOP-80")]
    public async Task TheServersRetryIntervalIsTheBaseOfTheWaitAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new Clock();
        var server = new Script(Stream("retry: 5000\n" + Opened), Stream(Opened), Unreachable);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var watch = Watch(server, Reconnection(clock), stop.Token);

        var everything = watch.MoveNextAsync();
        var afterRetry = await clock.Waits.Reader.ReadAsync(token);
        clock.Advance(afterRetry);
        Assert.True(await everything);
        var next = watch.MoveNextAsync();
        var afterPlain = await clock.Waits.Reader.ReadAsync(token);
        clock.Advance(afterPlain);
        var afterFailure = await clock.Waits.Reader.ReadAsync(token);
        await StopAsync(stop, watch, next);

        Assert.Equal([2.5, 2.5, 5], [afterRetry.TotalSeconds, afterPlain.TotalSeconds, afterFailure.TotalSeconds]);
    }

    [Fact(Timeout = 30_000)]
    [Trait("AC", "DESKTOP-81")]
    public async Task ARefusalThatRemainsAfterARefreshEndsTheWatchSignedOutAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new Clock();
        var server = new Script(Status(HttpStatusCode.Unauthorized), Status(HttpStatusCode.Unauthorized));
        var store = new SessionTokenStore();
        await store.WriteAsync(new TokenSet("a1", "r1", DateTimeOffset.MaxValue), token);
        using var tokens = new HttpClient(server, disposeHandler: false) { BaseAddress = Script.Address };
        var client = new SignInClient(new InstalledAppSignInOptions(), tokens, store, TimeProvider.System);
        using var http = new HttpClient(new SignedInHandler(client) { InnerHandler = server }) { BaseAddress = Script.Address };
        await using var watch = new RemoteLiveKeys(http, Reconnection(clock)).WatchAsync(["sales"], token).GetAsyncEnumerator(token);

        await Assert.ThrowsAsync<SignInRequiredException>(async () => await watch.MoveNextAsync());

        Assert.Equal(["/_live?keys=sales a1", "/connect/token", "/_live?keys=sales a2"], server.Requests);
        Assert.False(clock.Waits.Reader.TryRead(out _));
    }

    [Theory(Timeout = 30_000)]
    [Trait("AC", "DESKTOP-82")]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task RefusalsAndNoContentEndTheWatchAsync(HttpStatusCode status)
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new Clock();
        var server = new Script(Status(status));
        await using var watch = Watch(server, Reconnection(clock), token);

        Assert.False(await watch.MoveNextAsync());

        Assert.Single(server.Requests);
        Assert.False(clock.Waits.Reader.TryRead(out _));
    }

    [Fact(Timeout = 30_000)]
    [Trait("AC", "DESKTOP-83")]
    public async Task AWakeReconnectsAtOnceAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new Clock();
        var reconnection = Reconnection(clock);
        var server = new Script(Unreachable, Unreachable, Unreachable, Stream(Opened));
        await using var watch = Watch(server, reconnection, token);

        var everything = watch.MoveNextAsync();
        clock.Advance(await clock.Waits.Reader.ReadAsync(token));
        clock.Advance(await clock.Waits.Reader.ReadAsync(token));
        var third = await clock.Waits.Reader.ReadAsync(token);
        var woke = reconnection.Wake();

        Assert.True(await everything);
        Assert.True(woke);
        Assert.Equal(TimeSpan.FromSeconds(2), third);
        Assert.Equal(4, server.Requests.Count);
    }

    [Theory(Timeout = 30_000)]
    [Trait("AC", "DESKTOP-90")]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task TimeoutsAndTooManyRequestsRetryAfterTheBackoffAsync(HttpStatusCode status)
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new Clock();
        var server = new Script(Status(status), Stream(Opened));
        await using var watch = Watch(server, Reconnection(clock), token);

        var everything = watch.MoveNextAsync();
        var wait = await clock.Waits.Reader.ReadAsync(token);
        var before = server.Requests.Count;
        clock.Advance(wait);

        Assert.True(await everything);
        Assert.True(watch.Current.All);
        Assert.Equal(TimeSpan.FromSeconds(0.5), wait);
        Assert.Equal((1, 2), (before, server.Requests.Count));
    }

    [Theory(Timeout = 30_000)]
    [Trait("AC", "DESKTOP-91")]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task TheServersRetryAfterIsTheLeastWaitAndAWakeDoesNotCutItShortAsync(HttpStatusCode status)
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new Clock();
        var reconnection = Reconnection(clock);
        var server = new Script(RetryAfter(status, TimeSpan.FromSeconds(120)), Stream(Opened)) { Time = clock };
        await using var watch = Watch(server, reconnection, token);

        var everything = watch.MoveNextAsync();
        var wait = await clock.Waits.Reader.ReadAsync(token);
        var cut = reconnection.Wake();
        clock.Advance(TimeSpan.FromSeconds(119));
        var early = server.Requests.Count;
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.True(await everything);
        Assert.False(cut);
        Assert.Equal(TimeSpan.FromSeconds(120), wait);
        Assert.Equal(1, early);
        Assert.Equal([TimeSpan.Zero, TimeSpan.FromSeconds(120)], [.. server.Times.Select(time => time - server.Times[0])]);
    }

    [Fact(Timeout = 30_000)]
    [Trait("AC", "DESKTOP-92")]
    public async Task RetryAfterIsAWaitOfAtMostFiveMinutesAndNeverShortensTheBackoffAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new Clock();
        var reconnection = Reconnection(clock);
        var now = clock.GetUtcNow();

        Assert.Equal(TimeSpan.FromSeconds(90), reconnection.RetryAfter(new RetryConditionHeaderValue(now.AddSeconds(90))));
        Assert.Equal(TimeSpan.FromMinutes(5), reconnection.RetryAfter(new RetryConditionHeaderValue(TimeSpan.FromSeconds(600))));
        Assert.Equal(TimeSpan.Zero, reconnection.RetryAfter(new RetryConditionHeaderValue(now.AddSeconds(-5))));
        Assert.Equal(TimeSpan.Zero, reconnection.RetryAfter(null));

        var longer = reconnection.WaitAsync(TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(2), token);
        var floor = await clock.Waits.Reader.ReadAsync(token);
        clock.Advance(floor);
        await longer;
        var once = !clock.Waits.Reader.TryRead(out _);
        var plain = reconnection.WaitAsync(TimeSpan.FromSeconds(0.5), TimeSpan.Zero, token);
        var backoff = await clock.Waits.Reader.ReadAsync(token);
        clock.Advance(backoff);
        await plain;

        Assert.Equal([2, 0.5], [floor.TotalSeconds, backoff.TotalSeconds]);
        Assert.True(once);
    }

    private static async Task StopAsync(CancellationTokenSource stop, IAsyncEnumerator<Invalidation> watch, ValueTask<bool> pending)
    {
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
        await watch.DisposeAsync();
    }

    private static LiveReconnection Reconnection(Clock clock) => new(clock, static () => 0.5);

    private static IAsyncEnumerator<Invalidation> Watch(Script server, LiveReconnection reconnection, CancellationToken token) =>
        new RemoteLiveKeys(new HttpClient(server) { BaseAddress = Script.Address }, reconnection).WatchAsync(["sales"], token).GetAsyncEnumerator(token);

    private static Func<HttpResponseMessage> Status(HttpStatusCode status) => () => new HttpResponseMessage(status);

    private static Func<HttpResponseMessage> RetryAfter(HttpStatusCode status, TimeSpan wait) =>
        () => new HttpResponseMessage(status) { Headers = { RetryAfter = new RetryConditionHeaderValue(wait) } };

    private static HttpResponseMessage Unreachable() => throw new HttpRequestException("The server cannot be reached.");

    private static Func<HttpResponseMessage> Stream(string events) =>
        () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(events, Encoding.UTF8, "text/event-stream") };

    private sealed class Clock : TimeProvider
    {
        private readonly FakeTimeProvider _time = new();

        public Channel<TimeSpan> Waits { get; } = Channel.CreateUnbounded<TimeSpan>();

        public override long TimestampFrequency => _time.TimestampFrequency;

        public void Advance(TimeSpan delta) => _time.Advance(delta);

        public override DateTimeOffset GetUtcNow() => _time.GetUtcNow();

        public override long GetTimestamp() => _time.GetTimestamp();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = _time.CreateTimer(callback, state, dueTime, period);
            Waits.Writer.TryWrite(dueTime);
            return timer;
        }
    }

    private sealed class Script(params Func<HttpResponseMessage>[] answers) : HttpMessageHandler
    {
        public static readonly Uri Address = new("https://app.example.test/");

        private int _next;

        public List<string> Requests { get; } = [];

        public List<DateTimeOffset> Times { get; } = [];

        public TimeProvider Time { get; init; } = TimeProvider.System;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            if (request.RequestUri!.AbsolutePath == "/connect/token")
            {
                Requests.Add("/connect/token");
                var body = JsonSerializer.Serialize(new Dictionary<string, object> { ["access_token"] = "a2", ["refresh_token"] = "r2", ["expires_in"] = 900, ["token_type"] = "Bearer" });
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            }

            Times.Add(Time.GetUtcNow());
            Requests.Add(request.RequestUri.PathAndQuery + " " + (request.Headers.Authorization?.Parameter ?? "none"));
            return answers[_next++]();
        }
    }
}
