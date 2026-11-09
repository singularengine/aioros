using System.Reflection;
using System.Runtime.Loader;

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class TakenDispatcherTests
{
    [Fact]
    [Trait("AC", "ENGINE-291")]
    public async Task AnEngineWhoseDispatcherAnotherThreadTookRefusesToStartAsync()
    {
        var context = new IsolatedAvalonia();
        try
        {
            var brush = context.LoadFromAssemblyName(new AssemblyName("Avalonia.Base")).GetType("Avalonia.Media.SolidColorBrush", throwOnError: true)!;
            await Task.Factory.StartNew(
                () =>
                {
                    Thread.CurrentThread.Name = "a request";
                    Activator.CreateInstance(brush);
                },
                TestContext.Current.CancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            var host = context.LoadFromAssemblyName(new AssemblyName("Aioros.Avalonia")).GetType("Aioros.Avalonia.Hosting.AvaloniaHost", throwOnError: true)!;
            var start = (Task)host.GetMethod("StartAsync")!.Invoke(null, [typeof(object), true, null])!;

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => start);

            Assert.Contains("('a request')", error.Message, StringComparison.Ordinal);
            Assert.Contains("a page's model created an Avalonia object outside Avalonia's thread before the engine started", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            context.Unload();
        }
    }

    private sealed class IsolatedAvalonia() : AssemblyLoadContext("taken-dispatcher", isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName) =>
            assemblyName.Name is { } name && (name.StartsWith("Avalonia", StringComparison.Ordinal) || name.StartsWith("Aioros", StringComparison.Ordinal))
            && Path.Combine(AppContext.BaseDirectory, name + ".dll") is var path && File.Exists(path)
                ? LoadFromAssemblyPath(path)
                : null;
    }
}
