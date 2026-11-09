using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Channels;

namespace Showcase.Games;

public sealed record SeedWorker(int Index, long Rows, int Thread, string Phase);

public sealed record SeedSnapshot(string Phase, string Database, int Workers, long Rows, long Target, TimeSpan Elapsed, ImmutableArray<SeedWorker> Crew, ImmutableHashSet<int> Threads, bool Done)
{
    public static SeedSnapshot Starting { get; } = new("Starting", string.Empty, 0, 0, 0, TimeSpan.Zero, [], [], false);

    public double Share => Target == 0 ? (Done ? 1 : 0) : Math.Min(1, (double)Rows / Target);

    public double RowsPerSecond => Elapsed.TotalSeconds < 0.05 ? 0 : Rows / Elapsed.TotalSeconds;
}

/// <summary>Collects what the seeding workers report. One loop owns the numbers; everyone else reads snapshots.</summary>
public sealed class SeedProgress
{
    private readonly Channel<Action<Builder>> _reports = Channel.CreateUnbounded<Action<Builder>>(new UnboundedChannelOptions { SingleReader = true });
    private SeedSnapshot _snapshot = SeedSnapshot.Starting;

    public SeedSnapshot Snapshot => Volatile.Read(ref _snapshot);

    public void Begin(string database, int workers, long target) => _reports.Writer.TryWrite(builder =>
    {
        builder.Database = database;
        builder.Target = target;
        builder.Phase = "Seeding";
        builder.Clock = Stopwatch.StartNew();
        builder.Crew = [.. Enumerable.Range(0, workers).Select(index => new SeedWorker(index, 0, 0, "Waiting"))];
    });

    public void Report(int worker, long rows, string phase)
    {
        var thread = Environment.CurrentManagedThreadId;
        _reports.Writer.TryWrite(builder =>
        {
            builder.Rows += rows;
            builder.Threads = builder.Threads.Add(thread);
            if (worker < builder.Crew.Length)
            {
                var current = builder.Crew[worker];
                builder.Crew = builder.Crew.SetItem(worker, current with { Rows = current.Rows + rows, Thread = thread, Phase = phase });
            }
        });
    }

    public void Finish(string database, long rows, bool seeded) => _reports.Writer.TryWrite(builder =>
    {
        builder.Database = database;
        builder.Phase = seeded ? "Seeded" : "Loaded from the database";
        builder.Done = true;
        builder.Clock?.Stop();
        if (!seeded)
        {
            builder.Rows = rows;
            builder.Target = rows;
        }
    });

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var builder = new Builder();
        await foreach (var report in _reports.Reader.ReadAllAsync(cancellationToken))
        {
            report(builder);
            while (_reports.Reader.TryRead(out var next))
            {
                next(builder);
            }

            Volatile.Write(ref _snapshot, builder.Build());
        }
    }

    private sealed class Builder
    {
        public string Phase { get; set; } = "Starting";

        public string Database { get; set; } = string.Empty;

        public long Rows { get; set; }

        public long Target { get; set; }

        public Stopwatch? Clock { get; set; }

        public ImmutableArray<SeedWorker> Crew { get; set; } = [];

        public ImmutableHashSet<int> Threads { get; set; } = [];

        public bool Done { get; set; }

        public SeedSnapshot Build() => new(Phase, Database, Crew.Length, Rows, Target, Clock?.Elapsed ?? TimeSpan.Zero, Crew, Threads, Done);
    }
}
