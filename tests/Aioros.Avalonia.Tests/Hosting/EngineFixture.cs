using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Aioros.Avalonia.Hosting;
using Aioros.Avalonia.Tests.Hosting;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Tests.Pages;

[assembly: AssemblyFixture(typeof(EngineFixture))]

namespace Aioros.Avalonia.Tests.Hosting;

public sealed class EngineFixture : IAsyncLifetime
{
    private AvaloniaHost? _host;

    public AvaloniaPageEngine Engine { get; private set; } = null!;

    public AvaloniaHost Host => _host!;

    public XamlSources Sources { get; private set; } = null!;

    /// <summary>Where the XAML the tests load at run time is named from, as an app's project folder.</summary>
    public static string ProjectDirectory { get; } = Path.Combine(Path.GetTempPath(), "aioros-tests");

    public RecordedLogs Logs { get; } = new();

    public async ValueTask InitializeAsync()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        _host = await AvaloniaHost.StartAsync(typeof(TestApp), web: true, Logs.CreateLogger("Aioros.Pages"));
        Sources = new XamlSources(typeof(TestApp).Assembly, ProjectDirectory);
        Engine = new AvaloniaPageEngine(_host, Sources);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }

    public static PageDefinition Page(string name, params string[] layouts) =>
        new($"/{name}", $"Pages/{name}/Page.axaml", _ => null, [.. layouts.Select(layout => new LayoutDefinition(layout, _ => null))]);

    public static string BodyAt(CompiledPage page, string path, params object?[] models)
    {
        var writer = new ArrayBufferWriter<byte>();
        page.Plan.RenderBody(writer, [.. models], path);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    public static string Body(CompiledPage page, params object?[] models)
    {
        var writer = new ArrayBufferWriter<byte>();
        page.Plan.RenderBody(writer, [.. models]);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    public ValueTask<CompiledPage> RenderAsync(PageDefinition page, string theme, params object?[] models) =>
        Engine.RenderAsync(page, [.. models], theme, TestContext.Current.CancellationToken);

    public ValueTask<PostOutcome> PostAsync(PageDefinition page, object model, params (string Key, string Value)[] fields) =>
        Engine.PostAsync(page, [model], fields.ToImmutableDictionary(field => field.Key, field => field.Value), [], "light", TestContext.Current.CancellationToken);
}
