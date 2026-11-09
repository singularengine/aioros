using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Aioros.Generators.Pages;

/// <summary>
/// One view placed in a page: its instance key, the segment (layout or page) whose XAML holds it, the instance around it
/// (or <c>-1</c>), its source and class, and its view model with its loader once known.
/// </summary>
internal sealed class PlacedView(string key, int segment, int parent, string source, INamedTypeSymbol? type, (INamedTypeSymbol? Type, bool Live, PageLoader? Loader, string? Head) model = default, string? list = null)
{
    public string Key { get; } = key;

    public int Segment { get; } = segment;

    public int Parent { get; } = parent;

    public string Source { get; } = source;

    public INamedTypeSymbol? Class { get; } = type;

    public (INamedTypeSymbol? Type, bool Live, PageLoader? Loader, string? Head) Model { get; } = model;

    /// <summary>The code of the instance's <c>ViewList</c> when it repeats in a list's item template.</summary>
    public string? List { get; } = list;

    public PlacedView With((INamedTypeSymbol? Type, bool Live, PageLoader? Loader, string? Head) model) => new(Key, Segment, Parent, Source, Class, model, List);
}

/// <summary>
/// Finds the views each segment places, statically from the XAML: an element whose type is a view's <c>x:Class</c>, or an
/// <c>a:View</c> naming a view's file, nested views included, in document order. A source that names no file and a view
/// that places itself through others are <c>AIOROS018</c>, reported once at the file that places them. A view with a
/// loader in a list's item template repeats once per item (a per-instance loader there is <c>AIOROS020</c>, one that
/// cannot repeat <c>AIOROS021</c>); one without a loader takes the bound item from its list and is no instance; one with a
/// loader in any other template is chosen at run time (<c>AIOROS022</c>); one that sets its own <c>DataContext</c> keeps it, as
/// Avalonia does, and is no instance.
/// </summary>
internal sealed class ViewPlacements
{
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);
    private readonly SourceProductionContext _output;
    private readonly Dictionary<string, (string Path, string Text)> _sources;
    private readonly Compilation _compilation;
    private readonly Dictionary<string, XamlPage> _parsed;
    private readonly Dictionary<string, string> _classes;

    public ViewPlacements(SourceProductionContext output, Dictionary<string, (string Path, string Text)> sources, Compilation compilation)
    {
        _output = output;
        _sources = sources;
        _compilation = compilation;
        _parsed = sources.ToDictionary(file => file.Key, file => XamlPage.Parse(file.Value.Text), StringComparer.Ordinal);
        Views = [.. _parsed.Where(file => file.Value.Error is null && file.Value.DataType is not null).Select(file => file.Key)];
        _classes = Views
            .Where(view => _parsed[view].Class is { } name && compilation.GetTypeByMetadataName(name) is not null)
            .GroupBy(view => _parsed[view].Class!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    /// <summary>The files that are views, having a view model (<c>x:DataType</c>): a loader may sit beside each.</summary>
    public List<string> Views { get; }

    public List<PlacedView> Of(IEnumerable<string> segments, IReadOnlyDictionary<string, PageLoader> loaders)
    {
        var placed = new List<PlacedView>();
        var index = 0;
        foreach (var segment in segments)
        {
            Place(segment, index++, -1, segment + ":", [segment], placed, loaders);
        }

        return placed;
    }

    private void Place(string host, int segment, int parent, string prefix, List<string> path, List<PlacedView> placed, IReadOnlyDictionary<string, PageLoader> loaders)
    {
        if (!_parsed.TryGetValue(host, out var page))
        {
            return;
        }

        foreach (var reference in page.References())
        {
            if (Resolved(host, reference, path) is not { } view || Repeats(host, view, reference, loaders.TryGetValue(view, out var loader) ? loader : null) is not { } list)
            {
                continue;
            }

            var key = prefix + reference.Position;
            placed.Add(new PlacedView(key, segment, parent, view, _parsed[view].Class is { } name ? _compilation.GetTypeByMetadataName(name) : null, list: list.Length == 0 ? null : list));
            if (list.Length == 0)
            {
                Place(view, segment, placed.Count - 1, key + "/", [.. path, view], placed, loaders);
            }
        }
    }

    /// <summary>The view file a reference places, or nothing when it places none or is reported (a missing source, a cycle).</summary>
    private string? Resolved(string host, XamlReference reference, List<string> path)
    {
        var (position, source) = (reference.Position, reference.Source);
        var view = source is not null ? source.TrimStart('/') : _classes.TryGetValue(reference.Type!, out var known) ? known : null;
        if (view is null)
        {
            return null;
        }

        if (!_sources.ContainsKey(view) || !view.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase))
        {
            Report(Diagnostics.UnresolvedView, host, position, $"the a:View at {position} has the Source '{source}', which names no .axaml file of the project");
            return null;
        }

        if (path.Contains(view))
        {
            Report(Diagnostics.UnresolvedView, host, position, $"the view '{view}' at {position} places itself through {string.Join(" > ", path.SkipWhile(step => step != view).Append(view))}");
            return null;
        }

        return view;
    }

    /// <summary>
    /// Whether a reference is an instance: an empty string for one placed once, the code of its list for one that repeats,
    /// nothing for one that is not an instance (chosen at run time, supplied by its list, or reported).
    /// </summary>
    private string? Repeats(string host, string view, XamlReference reference, PageLoader? loader)
    {
        var at = $"the view '{view}' at {reference.Position}";
        if (reference.OwnContext)
        {
            return null;
        }

        if (reference.RunTime is not null)
        {
            if (loader is not null)
            {
                Report(Diagnostics.ChosenAtRunTime, host, reference.Position, $"{host}: {at} is placed in a template chosen at run time ({reference.RunTime}): it is not composed with the page, and its loader does not run with the page's");
            }

            return null;
        }

        if (reference.List is not { } list)
        {
            if (loader is { Batch: true })
            {
                Report(Diagnostics.Unrepeatable, host, reference.Position, $"{at} cannot repeat: it has a batch loader but is not placed in a list's item template");
                return null;
            }

            return string.Empty;
        }

        if (loader is null)
        {
            return null;
        }

        var (code, problem) = ViewLists.Of(_compilation, list, _parsed[host].DataType, reference.ItemKey, loader);
        if (problem is not null)
        {
            Report(Diagnostics.Unrepeatable, host, reference.Position, $"{at} cannot repeat: {problem}");
            return null;
        }

        if (!loader.Batch)
        {
            Report(Diagnostics.PerItemLoader, host, reference.Position, $"{at} is placed in the item template of the list at {list.Position} and its loader loads one instance, so it runs once per item (N+1 loads); let the list supply the item (remove the loader: the bound item is the view's model) or load every item at once with an IBatchLoader<TKey, TData>");
        }

        return code;
    }

    private void Report(DiagnosticDescriptor descriptor, string host, string position, string problem)
    {
        if (_reported.Add(host + "#" + position))
        {
            var location = Location.Create(_sources[host].Path, default, new LinePositionSpan(default, default));
            _output.ReportDiagnostic(descriptor.Id == Diagnostics.ChosenAtRunTime.Id ? Diagnostic.Create(descriptor, location, problem) : Diagnostic.Create(descriptor, location, host, problem));
        }
    }
}
