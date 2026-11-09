using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception);

public sealed class RecordedLogs : ILoggerProvider
{
    private ImmutableList<LogEntry> _entries = [];
    private TaskCompletionSource _added = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ImmutableList<LogEntry> Entries => Volatile.Read(ref _entries);

    public ILogger CreateLogger(string categoryName) => new Recorder(this, categoryName);

    public void Dispose()
    {
    }

    public async Task<LogEntry> WaitForAsync(Func<LogEntry, bool> match, CancellationToken cancellationToken)
    {
        while (true)
        {
            var added = Volatile.Read(ref _added).Task;
            if (Volatile.Read(ref _entries).Find(entry => match(entry)) is { } found)
            {
                return found;
            }

            await added.WaitAsync(cancellationToken);
        }
    }

    private void Add(LogEntry entry)
    {
        ImmutableInterlocked.Update(ref _entries, static (entries, added) => entries.Add(added), entry);
        Interlocked.Exchange(ref _added, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
    }

    private sealed class Recorder(RecordedLogs logs, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            logs.Add(new LogEntry(category, logLevel, formatter(state, exception), exception));
    }
}
