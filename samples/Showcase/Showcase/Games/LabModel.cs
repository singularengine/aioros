using System.Globalization;
using Aioros.Functions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Showcase.Games;

public sealed record WorkerRow(string Name, string Rows, string Thread, string Phase, double Share);

public sealed record CrewOption(string Label, string Count, bool Current, System.Windows.Input.ICommand Command);

/// <summary>The concurrency lab: a live view of the seeder and of the virtual players hitting the database.</summary>
public sealed partial class LabModel : ObservableObject
{
    private readonly ILabFunctions _lab;

    public LabModel(LabSnapshot data, ILabFunctions lab)
    {
        _lab = lab;
        Receive(data);
    }

    [ObservableProperty]
    public partial string Database { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DatabaseNote { get; set; } = string.Empty;

    public string Cores { get; } = Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty]
    public partial IReadOnlyList<CrewOption> Crew { get; set; } = [];

    [ObservableProperty]
    public partial string Players { get; set; } = "0";

    [ObservableProperty]
    public partial string Operations { get; set; } = "0";

    [ObservableProperty]
    public partial string Reads { get; set; } = "0";

    [ObservableProperty]
    public partial string Writes { get; set; } = "0";

    [ObservableProperty]
    public partial string P50 { get; set; } = "0 ms";

    [ObservableProperty]
    public partial string P99 { get; set; } = "0 ms";

    [ObservableProperty]
    public partial string Threads { get; set; } = "0";

    [ObservableProperty]
    public partial string Pool { get; set; } = "0";

    [ObservableProperty]
    public partial string Total { get; set; } = "0";

    [ObservableProperty]
    public partial string Errors { get; set; } = "0";

    [ObservableProperty]
    public partial IReadOnlyList<TrafficPoint> Throughput { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<TrafficPoint> Latency { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<FeedItem> Feed { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<TopSeller> Top { get; set; } = [];

    [ObservableProperty]
    public partial bool Quiet { get; set; } = true;

    [ObservableProperty]
    public partial string SeedPhase { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double SeedPercent { get; set; }

    [ObservableProperty]
    public partial string SeedSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SeedRate { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<WorkerRow> Workers { get; set; } = [];

    public void Receive(LabSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var now = snapshot.Traffic;
        Database = snapshot.Database;
        DatabaseNote = snapshot.Workers == 1
            ? "Running on SQLite: one writer at a time, and its .NET provider waits for the lock on a pool thread, so the lab offers a few players. Run Showcase.AppHost for PostgreSQL and up to 512."
            : "Running on PostgreSQL through Aspire, with a pool of 64 connections shared by every player.";
        Players = now.Players.ToString(CultureInfo.InvariantCulture);
        Crew = [.. snapshot.Crews.Select(size => new CrewOption(size == 0 ? "Stop" : size.ToString(CultureInfo.InvariantCulture), size.ToString(CultureInfo.InvariantCulture), size == now.Players, SetPlayersCommand))];
        Operations = now.PagesPerSecond.ToString("N0", CultureInfo.InvariantCulture);
        Reads = now.PostsPerSecond.ToString("N0", CultureInfo.InvariantCulture);
        Writes = now.MegabytesPerSecond.ToString("0.0", CultureInfo.InvariantCulture);
        P50 = now.P50.ToString("0.0", CultureInfo.InvariantCulture) + " ms";
        P99 = now.P99.ToString("0.0", CultureInfo.InvariantCulture) + " ms";
        Threads = now.Threads.ToString(CultureInfo.InvariantCulture);
        Pool = snapshot.Pool.ToString(CultureInfo.InvariantCulture);
        Total = now.Total.ToString("N0", CultureInfo.InvariantCulture);
        Errors = now.Errors.ToString("N0", CultureInfo.InvariantCulture);
        Throughput = now.Throughput;
        Latency = now.Latency;
        Feed = now.Feed;
        Top = now.Top;
        Quiet = now.Players == 0 && now.Feed.IsEmpty;

        var seeding = snapshot.Seed;
        SeedPhase = seeding.Phase;
        SeedPercent = Math.Round(seeding.Share * 100, 1);
        SeedSummary = string.Create(CultureInfo.InvariantCulture, $"{seeding.Rows:N0} of {seeding.Target:N0} rows · {seeding.Workers} {(seeding.Workers == 1 ? "worker" : "workers")} · {seeding.Threads.Count} threads used · {seeding.Elapsed.TotalSeconds:0.0} s");
        SeedRate = seeding.RowsPerSecond == 0 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"{seeding.RowsPerSecond:N0} rows/s");
        var most = Math.Max(1, seeding.Crew.IsEmpty ? 1 : seeding.Crew.Max(worker => worker.Rows));
        Workers = [.. seeding.Crew.Select(worker => new WorkerRow(
            "Worker " + (worker.Index + 1).ToString(CultureInfo.InvariantCulture),
            worker.Rows.ToString("N0", CultureInfo.InvariantCulture) + " rows",
            worker.Thread == 0 ? "–" : "thread " + worker.Thread.ToString(CultureInfo.InvariantCulture),
            seeding.Done ? "Done" : worker.Phase,
            100.0 * worker.Rows / most))];
    }

    [RelayCommand]
    private Task SetPlayersAsync(string? count) =>
        _lab.SetPlayersAsync(int.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out var players) ? players : 0);
}

/// <summary>Invalidates <c>lab</c> twice a second, so every live lab page reruns its loader and reads a fresh snapshot.</summary>
public sealed class LabFeed(IDataKeys keys, TimeProvider time) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            keys.Invalidate("lab");
        }
    }
}
