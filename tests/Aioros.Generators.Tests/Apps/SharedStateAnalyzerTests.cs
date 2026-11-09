using System.Collections.Immutable;
using System.Globalization;
using Aioros.CodeFixes.Services;
using Aioros.Generators.Tests.Pages;
using Microsoft.CodeAnalysis;

namespace Aioros.Generators.Tests.Apps;

public sealed class SharedStateAnalyzerTests
{
    [Fact]
    [Trait("AC", "GENERATOR-30")]
    public async Task SingletonViewModelsAreReportedAsync()
    {
        var diagnostics = await new GeneratorHarness()
            .Xaml("Pages/dashboard/Page.axaml", "<TextBlock />", "Dashboard")
            .Xaml("Pages/Layout.axaml", "<a:Outlet />", "Shell")
            .Xaml("Pages/settings/Page.axaml", "<TextBlock />", "Settings")
            .Source("""
                using App.Models;
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.Extensions.DependencyInjection.Extensions;

                namespace App.Models
                {
                    public interface IShell;

                    public sealed class Dashboard;

                    public sealed class Shell : IShell;

                    public sealed class Settings;

                    public sealed class Clock;
                }

                public static class Setup
                {
                    public static void Add(IServiceCollection services)
                    {
                        services.AddSingleton<Dashboard>();
                        services.TryAddSingleton(typeof(Shell));
                        services.AddKeyedSingleton<Settings>("main");
                        services.AddSingleton<IShell, Shell>();
                        services.AddScoped<Settings>();
                        services.AddTransient<Dashboard>();
                        services.AddSingleton<Clock>();
                    }
                }
                """)
            .AnalyzeAsync();

        var reported = diagnostics.Where(static diagnostic => diagnostic.Id == "AIOROS012").ToList();
        Assert.All(reported, static diagnostic => Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity));
        Assert.Equal(
            [
                (21, "'App.Models.Dashboard' is the view model of 'Pages/dashboard/Page.axaml'"),
                (22, "'App.Models.Shell' is the view model of 'Pages/Layout.axaml'"),
                (23, "'App.Models.Settings' is the view model of 'Pages/settings/Page.axaml'"),
                (24, "'App.Models.Shell' is the view model of 'Pages/Layout.axaml'"),
            ],
            reported.Select(static diagnostic => (diagnostic.Location.GetLineSpan().StartLinePosition.Line, Prefix(diagnostic))));
        Assert.All(reported, static diagnostic => Assert.Contains("one instance would serve every user and request on a server", Message(diagnostic), StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "GENERATOR-122")]
    public async Task SingletonViewModelsAreFixedToScopedAsync()
    {
        const string Setup = """
            using App.Models;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection.Extensions;

            namespace App.Models
            {
                public interface IShell;

                public sealed class Dashboard;

                public sealed class Shell : IShell;

                public sealed class Settings;
            }

            public static class Setup
            {
                public static void Add(IServiceCollection services)
                {
                    services.AddSingleton<Dashboard>();
                    services.TryAddSingleton(typeof(Shell));
                    services.AddKeyedSingleton<Settings>("main");
                    services.AddSingleton<IShell, Shell>();
                }
            }
            """;
        Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source) => new GeneratorHarness()
            .Xaml("Pages/dashboard/Page.axaml", "<TextBlock />", "Dashboard")
            .Xaml("Pages/Layout.axaml", "<a:Outlet />", "Shell")
            .Xaml("Pages/settings/Page.axaml", "<TextBlock />", "Settings")
            .Source(source)
            .AnalyzeAsync();
        var fix = new ScopedViewModelFix();

        var reported = (await AnalyzeAsync(Setup)).Where(static diagnostic => diagnostic.Id == "AIOROS012").OrderByDescending(static diagnostic => diagnostic.Location.SourceSpan.Start).ToList();
        var fixedLines = new List<string>();
        foreach (var diagnostic in reported)
        {
            var line = diagnostic.Location.GetLineSpan().StartLinePosition.Line;
            fixedLines.Add((await CodeFixRunner.ApplyAsync(fix, diagnostic)).Split('\n')[line].Trim());
        }

        var all = Setup;
        foreach (var diagnostic in reported)
        {
            var changed = (await AnalyzeAsync(all)).First(found => found.Id == "AIOROS012" && found.Location.SourceSpan == diagnostic.Location.SourceSpan);
            all = await CodeFixRunner.ApplyAsync(fix, changed);
        }

        Assert.Equal(
            ["services.AddScoped<IShell, Shell>();", "services.AddKeyedScoped<Settings>(\"main\");", "services.TryAddScoped(typeof(Shell));", "services.AddScoped<Dashboard>();"],
            fixedLines);
        Assert.Equal(Setup.Replace("Singleton", "Scoped", StringComparison.Ordinal), all);
        Assert.DoesNotContain(await AnalyzeAsync(all), static diagnostic => diagnostic.Id == "AIOROS012");
    }

    [Fact]
    [Trait("AC", "GENERATOR-31")]
    public async Task StaticMutableStateInViewModelsIsReportedAsync()
    {
        var diagnostics = await new GeneratorHarness()
            .Xaml("Pages/board/Page.axaml", "<TextBlock />", "Board")
            .Source("""
                using System.Collections.Concurrent;
                using System.Collections.Frozen;
                using System.Collections.Generic;
                using System.Collections.Immutable;
                using System.Collections.ObjectModel;

                namespace App.Models
                {
                    public abstract class Base
                    {
                        public static int Shared;
                    }

                    public sealed class Board : Base
                    {
                        public const int Size = 9;
                        private static int _count;
                        private static readonly List<string> Moves = [];
                        private static readonly IDictionary<string, int> Scores = new Dictionary<string, int>();
                        private static readonly ConcurrentDictionary<int, int> Cache = new();
                        private static readonly string[] Letters = ["a"];
                        private static readonly IReadOnlyList<string> Names = ["a"];
                        private static readonly ImmutableArray<int> Fixed = [];
                        private static readonly FrozenSet<int> Frozen = new[] { 1 }.ToFrozenSet();
                        private static readonly ReadOnlyCollection<int> Read = new([1]);
                        private static readonly object Gate = new();
                        private int _instance;

                        public static string Name { get; set; } = "";

                        public static int Computed => Size;

                        public int Instance { get; set; }
                    }

                    public sealed class Helper
                    {
                        private static int _other;
                    }
                }
                """)
            .AnalyzeAsync();

        var reported = diagnostics.Where(static diagnostic => diagnostic.Id == "AIOROS013").ToList();
        Assert.All(reported, static diagnostic => Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity));
        Assert.Equal(
            [
                "App.Models.Board: 'App.Models.Base.Shared' is a static field that is not readonly",
                "App.Models.Board: 'App.Models.Board.Cache' is a static readonly mutable collection",
                "App.Models.Board: 'App.Models.Board.Moves' is a static readonly mutable collection",
                "App.Models.Board: 'App.Models.Board.Name' is a static property with a setter",
                "App.Models.Board: 'App.Models.Board.Scores' is a static readonly mutable collection",
                "App.Models.Board: 'App.Models.Board._count' is a static field that is not readonly",
            ],
            reported.Select(static diagnostic => Message(diagnostic).Split(',')[0]).Order(StringComparer.Ordinal));
        Assert.All(reported, static diagnostic => Assert.Contains("every user shares it on a server", Message(diagnostic), StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "GENERATOR-32")]
    public async Task ProcessWideMessengersInViewModelsAreReportedAsync()
    {
        var diagnostics = await new GeneratorHarness()
            .Xaml("Pages/chat/Page.axaml", "<TextBlock />", "Chat")
            .Source("""
                namespace CommunityToolkit.Mvvm.Messaging
                {
                    public interface IMessenger;

                    public sealed class WeakReferenceMessenger : IMessenger
                    {
                        public static WeakReferenceMessenger Default { get; } = new();
                    }

                    public sealed class StrongReferenceMessenger : IMessenger
                    {
                        public static StrongReferenceMessenger Default { get; } = new();
                    }
                }

                namespace Splat
                {
                    public sealed class Locator
                    {
                        public static object Current { get; } = new();
                    }

                    public sealed class AppLocator
                    {
                        public static object Current { get; } = new();
                    }
                }

                namespace App.Models
                {
                    using CommunityToolkit.Mvvm.Messaging;

                    public sealed class Chat
                    {
                        private readonly IMessenger _messenger = WeakReferenceMessenger.Default;

                        public Chat()
                        {
                            _ = StrongReferenceMessenger.Default;
                            _ = Splat.Locator.Current;
                        }

                        public object Find() => Splat.AppLocator.Current;

                        public System.Func<IMessenger> Later() => () => WeakReferenceMessenger.Default;
                    }

                    public sealed class Service
                    {
                        public IMessenger Messenger => WeakReferenceMessenger.Default;
                    }
                }
                """)
            .AnalyzeAsync();

        var reported = diagnostics.Where(static diagnostic => diagnostic.Id == "AIOROS014").ToList();
        Assert.All(reported, static diagnostic => Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity));
        Assert.Equal(
            [
                (34, "App.Models.Chat: the view model uses 'CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default'"),
                (38, "App.Models.Chat: the view model uses 'CommunityToolkit.Mvvm.Messaging.StrongReferenceMessenger.Default'"),
                (39, "App.Models.Chat: the view model uses 'Splat.Locator.Current'"),
                (42, "App.Models.Chat: the view model uses 'Splat.AppLocator.Current'"),
                (44, "App.Models.Chat: the view model uses 'CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default'"),
            ],
            reported.Select(static diagnostic => (diagnostic.Location.GetLineSpan().StartLinePosition.Line, Message(diagnostic).Split(',')[0])).OrderBy(static entry => entry.Line));
        Assert.Contains("an IMessenger registered as scoped", Message(reported.MinBy(static diagnostic => diagnostic.Location.SourceSpan.Start)!), StringComparison.Ordinal);
    }

    private static string Message(Diagnostic diagnostic) => diagnostic.GetMessage(CultureInfo.InvariantCulture);

    private static string Prefix(Diagnostic diagnostic) => Message(diagnostic).Split(" and is registered", 2)[0];
}
