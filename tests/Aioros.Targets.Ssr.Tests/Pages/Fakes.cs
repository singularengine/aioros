using System.Collections.Immutable;
using System.ComponentModel;
using System.Text;
using System.Threading.Channels;
using Aioros.Navigation;
using Aioros.Pages;
using Aioros.Runtime.Rendering;
using Aioros.Targets.Ssr.Hosting;
using Aioros.Targets.Ssr.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

internal sealed class FakeEngine : IPageEngine
{
    public bool Reusable { get; set; } = true;

    public PostOutcome? Outcome { get; set; }

    public List<(string Theme, ImmutableArray<object?> Models)> Renders { get; } = [];

    /// <summary>The current culture and UI culture of each render and post, as <c>culture/ui</c>.</summary>
    public List<string> Cultures { get; } = [];

    public int Answers { get; private set; }

    public ImmutableDictionary<string, string>? Posted { get; private set; }

    public ImmutableArray<PostedFile> Files { get; private set; }

    public ImmutableArray<string?> Uploaded { get; private set; }

    public bool Fields { get; set; }

    public bool Uploads { get; set; }

    public string? Failure { get; set; }

    public RenderPlan? Plan { get; set; }

    public Func<PageDefinition, RenderPlan>? PlanFor { get; set; }

    public Func<ImmutableArray<object?>, ValueTask>? Command { get; set; }

    public bool Themed { get; set; }

    /// <summary>Whether the last post left its answer to the page's plan.</summary>
    public bool? PlanAnswered { get; private set; }

    /// <summary>Whether the engine runs Avalonia in the process, so posts pressing a button go through it.</summary>
    public bool OwnsCommands { get; set; }

    private bool? _runs;

    public bool RunsCommands
    {
        get => _runs ?? OwnsCommands;
        set => _runs = value;
    }

    private static string Culture() => System.Globalization.CultureInfo.CurrentCulture.Name + "/" + System.Globalization.CultureInfo.CurrentUICulture.Name;

    public static CompiledPage Page(bool reusable, bool fields = false, string? failure = null) => new(
        new RenderPlan([], [new LiteralNode("<p>"), new ValueNode(model => (model as IPageModel)?.Title), new LiteralNode("</p>"), .. fields ? new IPlanNode[] { IdNode.Instance } : []], []),
        ".page{}\n",
        "#fafafa",
        "#111",
        reusable,
        failure);

    public ValueTask<CompiledPage> RenderAsync(PageDefinition page, ImmutableArray<object?> models, string theme, CancellationToken cancellationToken)
    {
        Renders.Add((theme, models));
        Cultures.Add(Culture());
        var plan = PlanFor?.Invoke(page) ?? Plan;
        var compiled = (plan is null ? Page(Reusable, Fields, Failure) : Page(Reusable, Fields, Failure) with { Plan = plan }) with { Uploads = Uploads };
        return ValueTask.FromResult(Themed ? compiled with { Css = ":root{color-scheme:" + theme + "}\n" } : compiled);
    }

    public async ValueTask<CompiledPage> AnswerAsync(PageDefinition page, ImmutableArray<object?> models, string theme, CancellationToken cancellationToken)
    {
        Answers++;
        return await RenderAsync(page, models, theme, cancellationToken) with { Reusable = false, Failure = null };
    }

    public async ValueTask<PostOutcome> PostAsync(PageDefinition page, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, ImmutableArray<PostedFile> files, string theme, CancellationToken cancellationToken)
    {
        Posted = form;
        Cultures.Add(Culture());
        Files = files;
        Uploaded = [.. await Task.WhenAll(files.Select(async file => File.Exists(file.Path) ? await File.ReadAllTextAsync(file.Path, cancellationToken) : null))];
        if (Command is { } command)
        {
            await command(models);
        }

        return Outcome ?? new PostOutcome(PostStatus.Rendered, Plan is null ? Page(reusable: false) : Page(reusable: true) with { Plan = Plan });
    }

    public async ValueTask<PostOutcome> PostAsync(PageDefinition page, ImmutableArray<object?> models, ImmutableDictionary<string, string> form, ImmutableArray<PostedFile> files, string theme, bool planAnswers, CancellationToken cancellationToken)
    {
        PlanAnswered = planAnswers;
        var outcome = await PostAsync(page, models, form, files, theme, cancellationToken);
        return planAnswers && Outcome is null ? outcome with { Page = null } : outcome;
    }
}

internal sealed class Model(string title = "Inbox") : IPageModel, INotifyPropertyChanged
{
    private string _title = title;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        }
    }

    public bool NotFound { get; set; }

    public PageMetadata Metadata { get; set; } = PageMetadata.None;

    public string? RedirectTo { get; set; }

    public PageRequest? Request { get; private set; }

    public ValueTask LoadAsync(PageRequest request, CancellationToken cancellationToken)
    {
        Request = request;
        return ValueTask.CompletedTask;
    }
}

internal sealed class Mover : IPageModel
{
    private readonly INavigator _navigator;
    private readonly string? _loaded;

    public Mover(INavigator navigator, string title = "Mover", string? created = null, string? loaded = null)
    {
        _navigator = navigator;
        _loaded = loaded;
        Title = title;
        if (created is not null)
        {
            _ = navigator.NavigateAsync(new NavigationRequest(created), CancellationToken.None).AsTask();
        }
    }

    public string Title { get; }

    public string? RedirectTo { get; set; }

    public async ValueTask LoadAsync(PageRequest request, CancellationToken cancellationToken)
    {
        if (_loaded is { } path)
        {
            await _navigator.NavigateAsync(new NavigationRequest(path), cancellationToken);
        }
    }

    public ValueTask GoAsync(string path) => _navigator.NavigateAsync(new NavigationRequest(path), CancellationToken.None);

    public static Func<ImmutableArray<object?>, ValueTask> Command(params string[] paths) => async models =>
    {
        foreach (var path in paths)
        {
            await ((Mover)models[^1]!).GoAsync(path);
        }
    };
}

internal sealed class Site
{
    public Site(SsrOptions? options = null, IFieldResolver? fields = null, CrossSite.CrossOriginProtection? protection = null)
    {
        Options = options ?? new SsrOptions();
        Handler = new PageHandler(Engine, Cache, Options, fields, protection);
    }

    public FakeEngine Engine { get; } = new();

    public PlanCache Cache { get; } = new();

    public SsrOptions Options { get; }

    public PageHandler Handler { get; }

    public static PageDefinition Page(Func<object?> model, params Func<object?>[] layouts) =>
        new("/inbox", "Pages/inbox/Page.axaml", _ => model(), [.. layouts.Select(layout => new LayoutDefinition("Pages/Layout.axaml", _ => layout()))]);

    public static DefaultHttpContext Http(string? theme = null, Stream? body = null, IUiThread? ui = null, bool antiforgery = false, bool navigator = false)
    {
        var services = new ServiceCollection();
        if (navigator)
        {
            Aioros.Runtime.Pages.PageNavigatorRegistration.AddPageNavigator(services);
        }

        if (antiforgery)
        {
            services.AddLogging();
            services.AddAntiforgery();
        }

        if (ui is not null)
        {
            services.AddSingleton(ui);
        }

        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        http.Request.Path = "/inbox";
        http.Request.Scheme = "http";
        http.Request.Host = new HostString("app.test");
        http.Response.Body = body ?? new MemoryStream();
        if (theme is not null)
        {
            http.Request.Headers.Cookie = $"theme={theme}";
        }

        return http;
    }

    public static string Body(HttpContext http) => Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray());
}

internal sealed class FakeFields : IFieldResolver
{
    public FieldBinding? Binding(SlotSpec field) => field.Kind == "text" ? (scope, value) => ((Model)scope!).Title = value ?? ((Model)scope).Title : null;

    public FieldCommand? Command(SlotSpec field) => field.Kind == "command"
        ? new FieldCommand(_ => "go", (scope, _) =>
        {
            ((Model)scope!).RedirectTo = "/done";
            return ValueTask.CompletedTask;
        })
        : null;
}

internal sealed class Samples(params string[] urls) : IPrecompileSource
{
    public IAsyncEnumerable<string> UrlsAsync(CancellationToken cancellationToken) => urls.ToAsyncEnumerable();
}

internal sealed class Sitemap(params string[] urls) : ISitemapSource
{
    public async IAsyncEnumerable<string> UrlsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var url in urls)
        {
            await Task.Yield();
            yield return url;
        }
    }
}

internal sealed class FakeCapture : IPageCapture
{
    public (PageDefinition Page, ImmutableArray<object?> Models, CaptureOptions Options)? Last { get; private set; }

    public ValueTask<PageCapture> CaptureAsync(PageDefinition page, ImmutableArray<object?> models, CaptureOptions options, CancellationToken cancellationToken)
    {
        Last = (page, models, options);
        return ValueTask.FromResult(new PageCapture([0x89, 0x50, 0x4E, 0x47], [new LaidOutText("Hello", 10, 20, 30.5, 17)]));
    }
}

internal sealed class GatedUiThread : IUiThread
{
    private readonly TaskCompletionSource _open = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Waiting => _waiting.Task;

    public void Open() => _open.TrySetResult();

    public async ValueTask InvokeAsync(Action action, CancellationToken cancellationToken)
    {
        _waiting.TrySetResult();
        await _open.Task;
        action();
    }
}

internal sealed class SignalStream : Stream
{
    private readonly Channel<string> _texts = Channel.CreateUnbounded<string>();
    private readonly Channel<byte[]> _raw = Channel.CreateUnbounded<byte[]>();
    public ChannelReader<string> Writes => _texts.Reader;

    private int _writes;

    public ChannelReader<byte[]> Raw => _raw.Reader;

    public Action<int>? Written { get; set; }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => 0;

    public override long Position { get; set; }

    public override void Flush()
    {
    }

    public override void Write(byte[] buffer, int offset, int count) => Record(buffer.AsSpan(offset, count));

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Record(buffer.Span);
        return ValueTask.CompletedTask;
    }

    private void Record(ReadOnlySpan<byte> buffer)
    {
        _raw.Writer.TryWrite(buffer.ToArray());
        _texts.Writer.TryWrite(Encoding.UTF8.GetString(buffer));
        Written?.Invoke(++_writes);
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
