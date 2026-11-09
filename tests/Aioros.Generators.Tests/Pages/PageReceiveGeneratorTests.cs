using System.Collections.Immutable;
using System.Globalization;
using Aioros.Pages;

namespace Aioros.Generators.Tests.Pages;

public sealed class PageReceiveGeneratorTests
{
    private const string Data = """
        namespace App.Models;
        public sealed record IssueData(string Id);
        public sealed record ShellData(string User);
        """;

    private const string Recipient = """
        namespace CommunityToolkit.Mvvm.Messaging;
        public interface IRecipient<in TMessage> where TMessage : class { void Receive(TMessage message); }
        """;

    private static string Loader(string name, string data, string body) => $$"""
        using System.Threading;
        using System.Threading.Tasks;
        using Aioros.Pages;
        namespace App.Loaders;
        public sealed class {{name}} : ILoader<App.Models.{{data}}>
        {
            public ValueTask<LoaderResult<App.Models.{{data}}>> LoadAsync(PageRequest request, CancellationToken cancellationToken) =>
                ValueTask.FromResult<LoaderResult<App.Models.{{data}}>>(new App.Models.{{data}}({{body}}));
        }
        """;

    private static PageRequest Request(string id) =>
        new("/issues/" + id, ImmutableDictionary<string, string>.Empty.Add("id", id), ImmutableDictionary<string, string>.Empty);

    [Fact]
    [Trait("AC", "GENERATOR-23")]
    public async Task ViewModelsThatReceiveDataGetItPushedAsync()
    {
        var result = new GeneratorHarness()
            .Source(Data)
            .Source(Recipient)
            .Source("""
                namespace App.Models;
                public sealed class IssueModel(IssueData data) { public IssueData Data { get; private set; } = data; public void Receive(IssueData data) => Data = data; public void Receive(string other) { } }
                public sealed class ShellModel(ShellData data) : CommunityToolkit.Mvvm.Messaging.IRecipient<ShellData> { public ShellData Data { get; private set; } = data; void CommunityToolkit.Mvvm.Messaging.IRecipient<ShellData>.Receive(ShellData data) => Data = data; }
                public sealed class PlainModel(IssueData data) { public IssueData Data { get; } = data; }
                """)
            .Source(Loader("IssueLoader", "IssueData", "request.Value(\"id\")!"), "Pages/issues/[id]/Page.Loader.cs")
            .Source(Loader("ShellLoader", "ShellData", "request.Value(\"id\")!"), "Pages/Layout.Loader.cs")
            .Source(Loader("PlainLoader", "IssueData", "request.Value(\"id\")!"), "Pages/plain/[id]/Page.Loader.cs")
            .Xaml("Pages/Layout.axaml", "<a:Outlet />", "ShellModel")
            .Xaml("Pages/issues/[id]/Page.axaml", "<TextBlock />", "IssueModel")
            .Xaml("Pages/plain/[id]/Page.axaml", "<TextBlock />", "PlainModel")
            .Run();
        var pages = result.Pages(out _);
        var issue = pages.Single(page => page.Route == "/issues/{id}");
        var plain = pages.Single(page => page.Route == "/plain/{id}");

        var first = (await issue.Loader!.LoadAsync(new Services(), Request("7"), TestContext.Current.CancellationToken)).Data;
        var second = (await issue.Loader.LoadAsync(new Services(), Request("8"), TestContext.Current.CancellationToken)).Data;
        var model = issue.Loader.CreateModel(new Services(), first)!;
        issue.Loader.Receive!(model, second);
        var shellFirst = (await issue.Layouts[0].Loader!.LoadAsync(new Services(), Request("ada"), TestContext.Current.CancellationToken)).Data;
        var shellSecond = (await issue.Layouts[0].Loader!.LoadAsync(new Services(), Request("grace"), TestContext.Current.CancellationToken)).Data;
        var shell = issue.Layouts[0].Loader!.CreateModel(new Services(), shellFirst)!;
        issue.Layouts[0].Loader!.Receive!(shell, shellSecond);

        Assert.Empty(result.Diagnostics);
        Assert.Same(second, model.GetType().GetProperty("Data")!.GetValue(model));
        Assert.Same(shellSecond, shell.GetType().GetProperty("Data")!.GetValue(shell));
        Assert.Null(plain.Loader!.Receive);
    }

    [Fact]
    [Trait("AC", "GENERATOR-24")]
    public void AmbiguousOrWrongReceiveMethodsAreBuildErrors()
    {
        var diagnostics = new GeneratorHarness()
            .Source(Data)
            .Source(Recipient)
            .Source("""
                namespace App.Models;
                public sealed class Hidden(IssueData data) { private void Receive(IssueData data) { } }
                public sealed class Answering(IssueData data) { public bool Receive(IssueData data) => true; }
                public sealed class Wider(IssueData data) { public void Receive(IssueData data, int version) { } }
                public sealed class Twice(ShellData data) : CommunityToolkit.Mvvm.Messaging.IRecipient<ShellData> { public void Receive(ShellData data) { } void CommunityToolkit.Mvvm.Messaging.IRecipient<ShellData>.Receive(ShellData data) { } }
                """)
            .Source(Loader("HiddenLoader", "IssueData", "\"1\""), "Pages/hidden/Page.Loader.cs")
            .Source(Loader("AnsweringLoader", "IssueData", "\"1\""), "Pages/answering/Page.Loader.cs")
            .Source(Loader("WiderLoader", "IssueData", "\"1\""), "Pages/wider/Page.Loader.cs")
            .Source(Loader("TwiceLoader", "ShellData", "\"1\""), "Pages/twice/Page.Loader.cs")
            .Xaml("Pages/hidden/Page.axaml", "<TextBlock />", "Hidden")
            .Xaml("Pages/answering/Page.axaml", "<TextBlock />", "Answering")
            .Xaml("Pages/wider/Page.axaml", "<TextBlock />", "Wider")
            .Xaml("Pages/twice/Page.axaml", "<TextBlock />", "Twice")
            .Run()
            .Diagnostics;

        Assert.Equal(["AIOROS008", "AIOROS008", "AIOROS008", "AIOROS008"], diagnostics.Select(diagnostic => diagnostic.Id));
        Assert.Equal(
            ["/app/Pages/answering/Page.axaml", "/app/Pages/hidden/Page.axaml", "/app/Pages/twice/Page.axaml", "/app/Pages/wider/Page.axaml"],
            diagnostics.Select(diagnostic => diagnostic.Location.GetLineSpan().Path).Order(StringComparer.Ordinal));
        Assert.Contains(diagnostics, diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("App.Models.Twice", StringComparison.Ordinal) && diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("two", StringComparison.Ordinal));
        Assert.All(diagnostics, diagnostic => Assert.Contains("Receive", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal));
    }

    [Fact]
    [Trait("AC", "GENERATOR-39")]
    public void ViewModelsWithCommandsThatCannotReceiveTheirDataAreWarned()
    {
        var diagnostics = new GeneratorHarness()
            .Source(Data)
            .Source("""
                namespace CommunityToolkit.Mvvm.Input;
                [System.AttributeUsage(System.AttributeTargets.Method)] public sealed class RelayCommandAttribute : System.Attribute { }
                """)
            .Source("""
                namespace CommunityToolkit.Mvvm.ComponentModel;
                [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)] public sealed class ObservablePropertyAttribute : System.Attribute { }
                """)
            .Source("""
                using System.Windows.Input;
                namespace App.Models;
                public sealed class IssueModel(IssueData data) { public IssueData Data { get; } = data; public string Note { get; set; } = ""; public ICommand? Save { get; } }
                public sealed partial class ShellModel(ShellData data) { public ShellData Data { get; } = data; [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string _filter = ""; [CommunityToolkit.Mvvm.Input.RelayCommand] private void Refresh() { } }
                public sealed class KeptModel(IssueData data) { public IssueData Data { get; private set; } = data; public string Note { get; set; } = ""; public ICommand? Save { get; } public void Receive(IssueData data) => Data = data; }
                public sealed class ReadModel(IssueData data) { public IssueData Data { get; } = data; public string Note { get; set; } = ""; }
                public sealed class LeavingModel(IssueData data) { public IssueData Data { get; } = data; public ICommand? Leave { get; } }
                public sealed class FormModel { public string Note { get; set; } = ""; public ICommand? Save { get; } }
                """, "Models/Models.cs")
            .Source(Loader("ShellLoader", "ShellData", "\"ada\""), "Pages/Layout.Loader.cs")
            .Source(Loader("IssueLoader", "IssueData", "\"7\""), "Pages/issues/Page.Loader.cs")
            .Source(Loader("KeptLoader", "IssueData", "\"7\""), "Pages/kept/Page.Loader.cs")
            .Source(Loader("ReadLoader", "IssueData", "\"7\""), "Pages/read/Page.Loader.cs")
            .Source(Loader("LeavingLoader", "IssueData", "\"7\""), "Pages/leaving/Page.Loader.cs")
            .Xaml("Pages/Layout.axaml", "<a:Outlet />", "ShellModel")
            .Xaml("Pages/issues/Page.axaml", "<TextBlock />", "IssueModel")
            .Xaml("Pages/kept/Page.axaml", "<TextBlock />", "KeptModel")
            .Xaml("Pages/read/Page.axaml", "<TextBlock />", "ReadModel")
            .Xaml("Pages/leaving/Page.axaml", "<TextBlock />", "LeavingModel")
            .Xaml("Pages/form/Page.axaml", "<TextBlock />", "FormModel")
            .Run()
            .Diagnostics;
        var messages = diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).ToList();

        Assert.Equal(["AIOROS017", "AIOROS017"], diagnostics.Select(diagnostic => diagnostic.Id));
        Assert.All(diagnostics, diagnostic => Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, diagnostic.Severity));
        Assert.All(diagnostics, diagnostic => Assert.Equal("/app/Models/Models.cs", diagnostic.Location.GetLineSpan().Path));
        Assert.Contains(messages, message => message.Contains("'App.Models.ShellModel'", StringComparison.Ordinal));
        var issue = Assert.Single(messages, message => message.Contains("'App.Models.IssueModel'", StringComparison.Ordinal));
        Assert.Contains("Pages/issues/Page.axaml", issue, StringComparison.Ordinal);
        Assert.Contains("created again", issue, StringComparison.Ordinal);
        Assert.Contains("public void Receive(App.Models.IssueData)", issue, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "GENERATOR-65")]
    public void ViewModelsWhoseCommandsNavigateAreNotWarned()
    {
        const string Toolkit = """
            namespace CommunityToolkit.Mvvm.Input;
            [System.AttributeUsage(System.AttributeTargets.Method)] public sealed class RelayCommandAttribute : System.Attribute { }
            public sealed class RelayCommand(System.Action execute) : System.Windows.Input.ICommand
            {
                public event System.EventHandler? CanExecuteChanged { add { } remove { } }
                public bool CanExecute(object? parameter) => true;
                public void Execute(object? parameter) => execute();
            }
            """;
        const string Models = """
            using System.ComponentModel;
            using System.Threading;
            using System.Threading.Tasks;
            using System.Windows.Input;
            using Aioros.Navigation;
            using CommunityToolkit.Mvvm.Input;
            namespace App.Models;
            public interface IShopNavigator { Task GoToAsync(string path); }
            public static class Setup
            {
                public static Microsoft.Extensions.DependencyInjection.IServiceCollection Add(Microsoft.Extensions.DependencyInjection.IServiceCollection services) =>
                    Aioros.Runtime.Pages.NavigationInterfaceRegistration.AddNavigation<IShopNavigator>(services, Aioros.Generated.AiorosNavigators.All);
            }
            public interface INotRegistered { Task GoToAsync(string path); }
            public abstract class Validated : INotifyDataErrorInfo
            {
                public bool HasErrors { get; protected set; }
                public event System.EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged { add { } remove { } }
                public System.Collections.IEnumerable GetErrors(string? propertyName) => System.Array.Empty<string>();
                protected void Validate() => HasErrors = true;
            }
            public interface ISaver { Task SaveAsync(string value); }
            public sealed partial class FindModel(IssueData data, IShopNavigator navigator) { public IssueData Data { get; } = data; public string Search { get; set; } = ""; [RelayCommand] private Task FindAsync() => navigator.GoToAsync("/find?q=" + Search); }
            public sealed partial class PickModel(IssueData data, INavigator navigator) : Validated
            {
                public IssueData Data { get; } = data;
                public string Choice { get; set; } = "";
                public ICommand Clear { get; } = new RelayCommand(() => navigator.NavigateAsync(new NavigationRequest("/pick"), CancellationToken.None));
                [RelayCommand]
                private async Task SaveAsync()
                {
                    HasErrors = Choice.Length == 0;
                    if (HasErrors)
                    {
                        return;
                    }

                    Choice = Choice.Trim();
                    if (Choice == "a")
                    {
                        await navigator.NavigateAsync(new NavigationRequest("/a"), CancellationToken.None).ConfigureAwait(false);
                    }
                    else
                    {
                        await navigator.NavigateAsync(new NavigationRequest("/b"), CancellationToken.None);
                    }
                }
            }
            public sealed partial class RefusedModel(IssueData data, IShopNavigator navigator, ISaver saver) : Validated
            {
                public IssueData Data { get; } = data;
                public string Name { get; set; } = "";
                [RelayCommand]
                private async Task SaveAsync()
                {
                    try
                    {
                        await saver.SaveAsync(Name);
                    }
                    catch (System.ComponentModel.DataAnnotations.ValidationException)
                    {
                        Validate();
                        return;
                    }

                    await navigator.GoToAsync("/saved");
                }
            }
            public sealed partial class UncheckedModel(IssueData data, IShopNavigator navigator, ISaver saver) : Validated { public IssueData Data { get; } = data; public string Name { get; set; } = ""; [RelayCommand] private async Task SaveAsync() { try { await saver.SaveAsync(Name); } catch (System.ComponentModel.DataAnnotations.ValidationException) { return; } await navigator.GoToAsync("/saved"); } }
            public sealed partial class FailedModel(IssueData data, IShopNavigator navigator, ISaver saver) : Validated { public IssueData Data { get; } = data; public string Name { get; set; } = ""; [RelayCommand] private async Task SaveAsync() { try { await saver.SaveAsync(Name); } catch (System.InvalidOperationException) { Validate(); return; } await navigator.GoToAsync("/saved"); } }
            public sealed partial class EarlyModel(IssueData data, IShopNavigator navigator) { public IssueData Data { get; } = data; public string Search { get; set; } = ""; [RelayCommand] private async Task FindAsync() { if (Search.Length == 0) { Search = "all"; return; } await navigator.GoToAsync("/find"); } }
            public sealed partial class BranchModel(IssueData data, IShopNavigator navigator) { public IssueData Data { get; } = data; public string Search { get; set; } = ""; [RelayCommand] private async Task FindAsync() { if (Search.Length > 0) { await navigator.GoToAsync("/find"); } } }
            public sealed partial class HelperModel(IssueData data, IShopNavigator navigator) { public IssueData Data { get; } = data; public string Search { get; set; } = ""; [RelayCommand] private Task FindAsync() => GoAsync(); private Task GoAsync() => navigator.GoToAsync("/find"); }
            public sealed partial class OtherModel(IssueData data, INotRegistered navigator) { public IssueData Data { get; } = data; public string Search { get; set; } = ""; [RelayCommand] private Task FindAsync() => navigator.GoToAsync("/find"); }
            public sealed class UntracedModel(IssueData data, ICommand command) { public IssueData Data { get; } = data; public string Search { get; set; } = ""; public ICommand Find { get; } = command; }
            """;
        var harness = new GeneratorHarness().Source(Data).Source(Toolkit).Source(Models, "Models/Models.cs");
        string[] names = ["Find", "Pick", "Refused", "Unchecked", "Failed", "Early", "Branch", "Helper", "Other", "Untraced"];
        foreach (var name in names)
        {
            var folder = name.ToLowerInvariant();
            harness.Source(Loader(name + "Loader", "IssueData", "\"7\""), $"Pages/{folder}/Page.Loader.cs").Xaml($"Pages/{folder}/Page.axaml", "<TextBlock />", name + "Model");
        }

        var diagnostics = harness.Run().Diagnostics;

        Assert.All(diagnostics, diagnostic => Assert.Equal("AIOROS017", diagnostic.Id));
        Assert.Equal(
            ["App.Models.BranchModel", "App.Models.EarlyModel", "App.Models.FailedModel", "App.Models.HelperModel", "App.Models.OtherModel", "App.Models.UncheckedModel", "App.Models.UntracedModel"],
            diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture).Split('\'')[1]).Order(StringComparer.Ordinal));
    }
}
