using Aioros.Navigation;
using Aioros.ReactiveUI.Activation;
using Aioros.ReactiveUI.Navigation;
using Microsoft.CodeAnalysis;

namespace Aioros.ReactiveUI.Tests.Generators;

public sealed class ReactiveGeneratorTests
{
    private const string App = """
        using System;
        using System.Collections.Generic;
        using Microsoft.Extensions.DependencyInjection;
        using ReactiveUI;
        using ReactiveUI.Binding;
        using ReactiveUI.Primitives.Disposables;
        namespace App;
        public abstract class Base : ReactiveObject, IActivatableViewModel
        {
            protected Base(List<string> log) => this.WhenActivated(disposables =>
            {
                log.Add(GetType().Name + " on");
                disposables.Add(new ActionDisposable(() => log.Add(GetType().Name + " off")));
            });
            public ViewModelActivator Activator { get; } = new();
        }
        public sealed class Derived(List<string> log, IScreen screen) : Base(log), IRoutableViewModel
        {
            public string UrlPathSegment => "derived";
            public IScreen HostScreen { get; } = screen;
        }
        public sealed class Hidden : ReactiveObject, IActivatableViewModel, IScreen
        {
            public Hidden(List<string> log) => this.WhenActivated((MultipleDisposable _) => log.Add("Hidden on"));
            ViewModelActivator IActivatableViewModel.Activator { get; } = new();
            RoutingState IScreen.Router { get; } = new();
        }
        public sealed class Plain : ReactiveObject
        {
            public string Name { get; set; } = "";
        }
        public static class Setup
        {
            public static IServiceCollection Add(IServiceCollection services) => services.AddReactiveRoutes(routes => routes.Map<Derived>("/derived"));
        }
        """;

    [Fact]
    [Trait("AC", "REACTIVE-07")]
    public void ViewModelsAreFoundByTheirShape()
    {
        var (output, generated) = GeneratorHarness.Generate(App);
        var table = generated.Single(tree => tree.FilePath.EndsWith("AiorosReactiveViewModels.g.cs", StringComparison.Ordinal)).ToString();
        var assembly = GeneratorHarness.Load(output);
        var all = (ReactiveViewModels)assembly.GetType("Aioros.Generated.AiorosReactiveViewModels")!.GetProperty("All")!.GetValue(null)!;
        var log = new List<string>();
        var navigator = new Recorder();
        var page = new ReactivePage(navigator, new ReactiveRoutes().Map<Probe>("/probe"), all);
        var hidden = Activator.CreateInstance(assembly.GetType("App.Hidden")!, log)!;
        var derived = Activator.CreateInstance(assembly.GetType("App.Derived")!, log, hidden)!;
        var plain = Activator.CreateInstance(assembly.GetType("App.Plain")!)!;

        var plainEnd = all.Attach(plain, page);
        var hiddenEnd = all.Attach(hidden, page);
        var derivedEnd = all.Attach(derived, page);
        var shown = ((global::ReactiveUI.IScreen)hidden).Router.NavigationStack.ToList();
        derivedEnd!.Dispose();
        var (_, bare) = GeneratorHarness.Generate("namespace App; public sealed class Plain { }", reactive: false);

        Assert.Empty(output.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.True(table.IndexOf("case global::App.Derived", StringComparison.Ordinal) < table.IndexOf("case global::App.Base", StringComparison.Ordinal));
        Assert.DoesNotContain("App.Plain", table, StringComparison.Ordinal);
        Assert.Contains(generated, tree => tree.ToString().Contains("AddReactiveRoutes(", StringComparison.Ordinal));
        Assert.DoesNotContain("System.Reflection", string.Concat(generated.Select(tree => tree.ToString())), StringComparison.Ordinal);
        Assert.Null(plainEnd);
        Assert.NotNull(hiddenEnd);
        Assert.Equal(["Hidden on", "Derived on", "Derived off"], log);
        Assert.Equal([derived], shown);
        Assert.Empty(bare);
    }

    [Fact]
    [Trait("AC", "REACTIVE-08")]
    public async Task InteractionsOnlyANativeViewHandlesAreReportedAsync()
    {
        var diagnostics = await GeneratorHarness.AnalyzeAsync("""
            using ReactiveUI;
            using ReactiveUI.Binding;
            namespace App;
            public sealed class StoreViewModel : ReactiveObject
            {
                private readonly Interaction<string, bool> _confirm = new();
                public StoreViewModel() => Answered.RegisterHandler(context => context.SetOutput(true));
                public Interaction<string, string?> ShowDialog { get; } = new();
                public Interaction<string, bool> Answered { get; } = new();
                public Interaction<string, bool> Confirm => _confirm;
            }
            """);

        var reported = diagnostics.Where(diagnostic => diagnostic.Id == "AIOROSRX001").ToList();

        Assert.Equal(["Confirm", "ShowDialog"], reported.Select(diagnostic => diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Split('\'')[1]).Order(StringComparer.Ordinal));
        Assert.All(reported, diagnostic => Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity));
        Assert.All(reported, diagnostic => Assert.Contains("'StoreViewModel'", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
    }

    private sealed class Recorder : INavigator
    {
        public List<string> Paths { get; } = [];

        public ValueTask NavigateAsync(NavigationRequest request, CancellationToken cancellationToken)
        {
            Paths.Add(request.Path);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Probe : global::ReactiveUI.ReactiveObject, global::ReactiveUI.IRoutableViewModel
    {
        public string? UrlPathSegment => "probe";

        public global::ReactiveUI.IScreen HostScreen => throw new NotSupportedException();
    }
}
