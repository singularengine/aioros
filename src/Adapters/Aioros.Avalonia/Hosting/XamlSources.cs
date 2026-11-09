using Aioros.Markup;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Aioros.Avalonia.Hosting;

/// <summary>
/// The app's XAML: its compiled pages, the edits hot reload has loaded over them, and XAML loaded at run time, each element
/// with its position in its file (<c>XamlSourceInfo</c>), which runs from <paramref name="projectDirectory"/> when given.
/// </summary>
public sealed partial class XamlSources(Assembly assembly, string? projectDirectory = null)
{
    static XamlSources() => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Web).TypeHandle);

    private ImmutableDictionary<string, string> _overrides = ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal);
    private ImmutableDictionary<string, Control> _prepared = ImmutableDictionary<string, Control>.Empty.WithComparers(StringComparer.Ordinal);

    public Assembly Assembly { get; } = assembly;

    public Control Load(string source)
    {
        if (ImmutableInterlocked.TryRemove(ref _prepared, source, out var prepared))
        {
            return prepared;
        }

        return _overrides.TryGetValue(source, out var xaml)
            ? Parse(xaml, source)
            : (Control)AvaloniaXamlLoader.Load(Address(source));
    }

    /// <summary>
    /// Loads XAML at run time with its elements' positions (<c>XamlSourceInfo</c>, as the build's XAML compiler records them
    /// in Aioros apps), named after <paramref name="source"/>, so a plan that fails names the line that made it fail.
    /// </summary>
    public Control Parse(string xaml, string? source = null) => (Control)ParseAny(xaml, source);

    public object ParseAny(string xaml, string? source = null) =>
        AvaloniaRuntimeXamlLoader.Load(new RuntimeXamlLoaderDocument(source is null ? null : Address(source), ForRuntime(xaml)) { Document = File(source) }, Configuration);

    public void Populate(string xaml, object root) =>
        AvaloniaRuntimeXamlLoader.Load(new RuntimeXamlLoaderDocument(null, root, ForRuntime(xaml)), Configuration);

    private RuntimeXamlLoaderConfiguration Configuration => field ??= new() { LocalAssembly = Assembly, CreateSourceInfo = true };

    private Uri Address(string source) => new($"avares://{Assembly.GetName().Name}/{source}");

    /// <summary>The file a source is loaded from, by its full path, as the build's XAML compiler names it.</summary>
    private string? File(string? source) =>
        source is null ? null : Path.GetFullPath(Path.Combine(projectDirectory ?? Path.GetTempPath(), source));

    /// <summary>
    /// Names the app's assembly in each <c>using:</c> namespace the app defines types in. Avalonia's run-time loader
    /// resolves the types of compiled-binding casts (<c>((m:Model)DataContext)</c>) only from namespaces that name their
    /// assembly, which the build's XAML compiler does not need; both forms mean the same namespace.
    /// </summary>
    private string ForRuntime(string xaml) =>
        UsingNamespace().Replace(xaml, match => Namespaces.Contains(match.Groups["ns"].Value)
            ? $"=\"clr-namespace:{match.Groups["ns"].Value};assembly={Assembly.GetName().Name}\""
            : match.Value);

    private FrozenSet<string> Namespaces => field ??= Assembly.GetTypes().Select(type => type.Namespace ?? string.Empty).ToFrozenSet(StringComparer.Ordinal);

    [GeneratedRegex("=\"using:(?<ns>[A-Za-z_][\\w.]*)\"")]
    private static partial Regex UsingNamespace();

    /// <summary>
    /// Loads an edited source over the compiled one: a page, layout or view is loaded from it from now on, and
    /// <c>App.axaml</c> replaces the application's styles and resources; the parser's message when it does not parse.
    /// </summary>
    public string? Reload(string source, string xaml)
    {
        object parsed;
        try
        {
            parsed = ParseAny(xaml, source);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return error.Message;
        }

        if (parsed is Application && Application.Current is { } application)
        {
            application.Styles.Clear();
            application.Resources = new ResourceDictionary();
            Populate(xaml, application);
        }
        else
        {
            Override(source, xaml, parsed as Control);
        }

        return null;
    }

    /// <summary>Whether an edited source changes the looks other XAML takes: the application, or a style or a control theme.</summary>
    public static bool Restyles(string xaml) =>
        xaml.Contains("<Application", StringComparison.Ordinal) || xaml.Contains("<Style", StringComparison.Ordinal) || xaml.Contains("ControlTheme", StringComparison.Ordinal);

    public bool IsCurrent(string source, string xaml) => _overrides.TryGetValue(source, out var current) && current == xaml;

    public void Override(string source, string xaml, Control? prepared = null)
    {
        ImmutableInterlocked.AddOrUpdate(ref _overrides, source, xaml, (_, _) => xaml);
        if (prepared is not null)
        {
            ImmutableInterlocked.AddOrUpdate(ref _prepared, source, prepared, (_, _) => prepared);
        }
    }
}
