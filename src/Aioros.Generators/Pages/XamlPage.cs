using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Aioros.Generators.Pages;

internal sealed class XamlPage
{
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string AiorosNamespace = "https://aioros.dev";
    private const string AvaloniaNamespace = "https://github.com/avaloniaui";
    private const string OnPlatformMarkup = "{OnPlatform";

    private XamlPage(string? dataType, bool live, string? error, string? head = null, XElement? root = null)
    {
        DataType = dataType;
        Live = live;
        Error = error;
        Head = head;
        _root = root;
        Class = root?.Attribute(XName.Get("Class", XamlNamespace))?.Value.Trim();
        Platforms = root?.Attribute(XName.Get("Page.Platforms", AiorosNamespace))?.Value;
        UsesAvaloniaOnPlatform = root is not null && root.DescendantsAndSelf().Any(element =>
            (element.Name.LocalName == "OnPlatform" && element.Name.NamespaceName == AvaloniaNamespace)
            || element.Attributes().Any(attribute => NamesOnPlatform(attribute.Value)));
    }

    /// <summary>The platforms the page's root declares (<c>a:Page.Platforms</c>), as written.</summary>
    public string? Platforms { get; }

    /// <summary>The file uses Avalonia's own <c>OnPlatform</c>, which the web would evaluate on the server's operating system.</summary>
    public bool UsesAvaloniaOnPlatform { get; }

    private readonly XElement? _root;

    /// <summary>The class the file defines (<c>x:Class</c>), which other files place by its element name.</summary>
    public string? Class { get; }

    public string? Head { get; }

    public string? DataType { get; }

    public bool Live { get; }

    public string? Error { get; }

    public static XamlPage Parse(string text)
    {
        XElement root;
        try
        {
            root = XDocument.Parse(text).Root!;
        }
        catch (XmlException error)
        {
            return new XamlPage(null, false, error.Message);
        }

        var live = root.Attributes().Any(attribute => attribute.Name.NamespaceName == AiorosNamespace
            && attribute.Name.LocalName == "Web.Live"
            && string.Equals(attribute.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase));
        var head = HeadOf(root);
        var dataType = root.Attribute(XName.Get("DataType", XamlNamespace))?.Value;
        if (dataType is null)
        {
            return new XamlPage(null, live, null, head, root);
        }

        var resolved = Resolve(root, dataType.Trim());
        return resolved is null
            ? new XamlPage(dataType, live, $"x:DataType '{dataType}' does not name a type in a 'using:' or 'clr-namespace:' namespace", head, root)
            : new XamlPage(resolved, live, null, head, root);
    }

    /// <summary>
    /// The CLR names of every type the file names as a data type: <c>x:DataType</c> on any element (also as
    /// <c>{x:Type}</c>) and a <c>DataTemplate</c>'s <c>DataType</c>, each resolved from the namespaces in scope there.
    /// </summary>
    public IEnumerable<string> DataTypes() => _root is null
        ? []
        : _root.DescendantsAndSelf()
            .SelectMany(element => new[] { element.Attribute(XName.Get("DataType", XamlNamespace)), element.Name.LocalName == "DataTemplate" ? element.Attribute("DataType") : null }
                .Select(attribute => Resolve(element, attribute?.Value.Trim())))
            .OfType<string>();

    /// <summary>
    /// The member paths of every binding in the file: a <c>{Binding}</c> or <c>{CompiledBinding}</c> attribute and a
    /// <c>Binding</c> element's <c>Path</c>, whatever their scope.
    /// </summary>
    public IEnumerable<string> BindingPaths() => _root is null
        ? []
        : _root.DescendantsAndSelf().SelectMany(element => element.Attributes()
            .Select(attribute => element.Name.LocalName is "Binding" or "CompiledBinding" && attribute.Name.LocalName == "Path"
                ? (Member(attribute.Value.Trim()) ? attribute.Value.Trim() : null)
                : Path(attribute.Value)))
            .OfType<string>();

    /// <summary>
    /// The member path of every <c>Command</c> binding of the file whose data type is known where it is written, with that
    /// data type's CLR name: the nearest <c>x:DataType</c>, or a template's <c>DataType</c>; a template without one, a
    /// <c>DataContext</c> set on the way, or a binding with another source or a converter leaves the binding out.
    /// </summary>
    public IEnumerable<(string DataType, string Path)> CommandBindings() => _root is null
        ? []
        : _root.DescendantsAndSelf().SelectMany(element => element.Attributes()
            .Where(attribute => attribute.Name.LocalName == "Command" || attribute.Name.LocalName.EndsWith(".Command", StringComparison.Ordinal))
            .Where(attribute => Sourceless(attribute.Value))
            .Select(attribute => (DataType: Scope(element), Path: Path(attribute.Value)))
            .Where(found => found.DataType is not null && found.Path is not null)
            .Select(found => (found.DataType!, found.Path!)));

    /// <summary>
    /// The <c>Command</c> bindings of the file that reach the page's own model from inside its templates, the way Avalonia
    /// writes it, <c>$parent[UserControl].((vm:BoardModel)DataContext).PlaceCommand</c>: the root element's type named in
    /// brackets and its <c>x:DataType</c> named in the cast, with the member path after it and the number of templates the
    /// binding is written in, the depth of the outer scope the plan reads it from (<c>^1.PlaceCommand</c>).
    /// </summary>
    public IEnumerable<(string DataType, string Path, int Depth)> PageCommandBindings()
    {
        if (_root is null || _root.Attribute(XName.Get("DataType", XamlNamespace)) is not { } rooted || Resolve(_root, rooted.Value.Trim()) is not { } page)
        {
            return [];
        }

        return _root.Descendants().SelectMany(element => element.Attributes()
            .Where(attribute => attribute.Name.LocalName == "Command" || attribute.Name.LocalName.EndsWith(".Command", StringComparison.Ordinal))
            .Select(attribute => ParentPath(_root, element, attribute.Value, page))
            .OfType<string>()
            .Select(path => (page, path, element.Ancestors().Count(ancestor => ancestor.Name.LocalName is "DataTemplate" or "TreeDataTemplate")))
            .Where(found => found.Item3 > 0));
    }

    private static string? ParentPath(XElement root, XElement element, string binding, string page)
    {
        var text = binding.Trim();
        var open = text.StartsWith("{Binding ", StringComparison.Ordinal) ? 9 : text.StartsWith("{CompiledBinding ", StringComparison.Ordinal) ? 17 : -1;
        if (open < 0 || !text.EndsWith("}", StringComparison.Ordinal))
        {
            return null;
        }

        var path = text.Substring(open, text.Length - open - 1).Trim();
        path = path.StartsWith("Path=", StringComparison.Ordinal) ? path.Substring(5) : path;
        var prefix = "$parent[" + root.Name.LocalName + "].((";
        var cast = path.IndexOf(")DataContext).", StringComparison.Ordinal);
        if (!path.StartsWith(prefix, StringComparison.Ordinal) || cast < 0 || path.IndexOf(',') >= 0
            || Resolve(element, path.Substring(prefix.Length, cast - prefix.Length)) != page)
        {
            return null;
        }

        var member = path.Substring(cast + ")DataContext).".Length);
        return Member(member) ? member : null;
    }

    private static readonly string[] OtherSources = ["ElementName", "RelativeSource", "Source=", "Converter"];

    private static bool Sourceless(string binding) => OtherSources.All(other => binding.IndexOf(other, StringComparison.Ordinal) < 0);

    /// <summary>The data type a binding written on this element reads from, or nothing when it cannot be told from the file.</summary>
    private static string? Scope(XElement element)
    {
        foreach (var current in element.AncestorsAndSelf())
        {
            if (current.Attribute(XName.Get("DataType", XamlNamespace)) is { } dataType)
            {
                return Resolve(current, dataType.Value.Trim());
            }

            if (current.Name.LocalName.EndsWith("Template", StringComparison.Ordinal))
            {
                return Resolve(current, current.Attribute("DataType")?.Value.Trim());
            }

            if (current.Attribute("DataContext") is not null)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// The elements that may place a view, in document order, with their position (the element indexes from the root):
    /// an <c>a:View</c> with its <c>Source</c>, or an element whose CLR type name is resolved from its namespace. One inside
    /// a list's item template carries that list; one inside another template, a style or resources carries the name of
    /// what holds it, since what those create is chosen at run time.
    /// </summary>
    public IReadOnlyList<XamlReference> References()
    {
        var scan = new Scan();
        if (_root is not null)
        {
            Collect(_root, string.Empty, scan, new Context(null, null, false));
        }

        return scan.Found;
    }

    private sealed class Scan
    {
        public List<XamlReference> Found { get; } = [];

        public Dictionary<XElement, (int Ordinal, string Position)> Lists { get; } = [];
    }

    private sealed class Context(XamlList? list, string? runTime, bool contextChanged)
    {
        public XamlList? List { get; } = list;

        public string? RunTime { get; } = runTime;

        public bool ContextChanged { get; } = contextChanged;
    }

    private static void Collect(XElement element, string position, Scan scan, Context context)
    {
        var index = 0;
        foreach (var child in element.Elements())
        {
            var at = position.Length == 0 ? index.ToString(System.Globalization.CultureInfo.InvariantCulture) : position + "." + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            index++;
            if (context.List is null && context.RunTime is null && HasItemTemplate(child))
            {
                scan.Lists[child] = (scan.Lists.Count, at);
            }

            if (Template(element, child, context, scan) is { } inner)
            {
                Collect(child, at, scan, inner);
                continue;
            }

            var reference = Reference(child, at, context);
            if (reference is not null)
            {
                scan.Found.Add(reference);
            }

            if (reference?.Source is null)
            {
                Collect(child, at, scan, Within(child, context));
            }
        }
    }

    /// <summary>The context inside a child: marked once an element outside every template sets its own <c>DataContext</c>.</summary>
    private static Context Within(XElement child, Context context) =>
        context.ContextChanged || context.List is not null || child.Attribute("DataContext") is null ? context : new Context(context.List, context.RunTime, true);

    /// <summary>
    /// The context inside a child that is a template: a list's item template and the <c>DataTemplate</c> in it carry the
    /// list, any other template, style or resources the name of what is chosen at run time.
    /// </summary>
    private static Context? Template(XElement element, XElement child, Context context, Scan scan)
    {
        var name = child.Name.LocalName;
        if (name.EndsWith(".ItemTemplate", StringComparison.Ordinal) && context.RunTime is null)
        {
            return new Context(ListOf(element, scan, context), null, context.ContextChanged);
        }

        if (context.List is { ItemType: null } list && element.Name.LocalName.EndsWith(".ItemTemplate", StringComparison.Ordinal))
        {
            return new Context(list.Typed(Resolve(child, child.Attribute(XName.Get("DataType", XamlNamespace))?.Value.Trim())), null, context.ContextChanged);
        }

        return Skipped(name) ? new Context(context.List, context.RunTime ?? name, context.ContextChanged) : null;
    }

    private static XamlReference? Reference(XElement child, string at, Context context)
    {
        var name = child.Name.LocalName;
        if (child.Name.NamespaceName == AiorosNamespace && name == "View")
        {
            return new XamlReference(at, null, child.Attribute("Source")?.Value.Trim() ?? string.Empty, context.List, context.RunTime, ItemKey(child), OwnContext(child));
        }

        return name.IndexOf('.') < 0 && Clr(child.Name.NamespaceName) is { } space
            ? new XamlReference(at, space + "." + name, null, context.List, context.RunTime, ItemKey(child), OwnContext(child))
            : null;
    }

    private static bool OwnContext(XElement element) =>
        element.Attribute("DataContext") is not null || element.Elements().Any(child => child.Name.LocalName.EndsWith(".DataContext", StringComparison.Ordinal));

    private static bool Skipped(string name) =>
        name.Contains("Template") || name.EndsWith("Styles", StringComparison.Ordinal) || name.EndsWith("Resources", StringComparison.Ordinal)
        || name.EndsWith("DataTemplates", StringComparison.Ordinal) || name is "Style" or "ControlTheme";

    private static XamlList ListOf(XElement owner, Scan scan, Context context)
    {
        if (context.List is not null || !scan.Lists.TryGetValue(owner, out var list))
        {
            return new XamlList(-1, string.Empty, null, nested: true, contextChanged: context.ContextChanged);
        }

        var binding = owner.Attribute("ItemsSource")?.Value.Trim();
        return new XamlList(list.Ordinal, list.Position, Path(binding), nested: false, contextChanged: context.ContextChanged, binding: binding);
    }

    private static bool NamesOnPlatform(string value)
    {
        for (var at = value.IndexOf(OnPlatformMarkup, StringComparison.Ordinal); at >= 0; at = value.IndexOf(OnPlatformMarkup, at + 1, StringComparison.Ordinal))
        {
            var next = at + OnPlatformMarkup.Length;
            if (next == value.Length || char.IsWhiteSpace(value[next]) || value[next] == '}')
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasItemTemplate(XElement element) =>
        element.Attribute("ItemTemplate") is not null || element.Elements().Any(child => child.Name.LocalName.EndsWith(".ItemTemplate", StringComparison.Ordinal));

    /// <summary>The element's <c>a:View.ItemKey</c>, a member path of the item (<c>Id</c>, <c>Owner.Id</c>), and the value as written.</summary>
    private static (string? Path, string Binding)? ItemKey(XElement element) =>
        element.Attribute(XName.Get("View.ItemKey", AiorosNamespace))?.Value.Trim() is { } written ? (Member(written) ? written : null, written) : null;

    /// <summary>The member path of a binding (<c>{Binding Cards}</c>, <c>{Binding Path=Board.Cards}</c>, <c>{CompiledBinding Cards}</c>), or nothing for any other value.</summary>
    internal static string? Path(string? binding)
    {
        if (binding is null)
        {
            return null;
        }

        var text = binding.Trim();
        if (!text.StartsWith("{", StringComparison.Ordinal) || !text.EndsWith("}", StringComparison.Ordinal))
        {
            return null;
        }

        text = text.Substring(1, text.Length - 2).Trim();
        var space = text.IndexOf(' ');
        var kind = space < 0 ? text : text.Substring(0, space);
        if (kind is not ("Binding" or "CompiledBinding") || space < 0)
        {
            return null;
        }

        var path = text.Substring(space + 1).Split(',')[0].Trim();
        path = path.StartsWith("Path=", StringComparison.Ordinal) ? path.Substring(5).Trim() : path;
        return Member(path) ? path : null;
    }

    /// <summary>A member path, each member followed by any integer indexes of the list it holds (<c>Items[1].Name</c>).</summary>
    private static bool Member(string path) =>
        path.Length > 0 && path.Split('.').All(part => Name(Plans.PlanAccessors.Indexed(part, out _)) && part.Length > 0);

    private static bool Name(string part) =>
        part.Length > 0 && (char.IsLetter(part[0]) || part[0] == '_') && part.All(character => char.IsLetterOrDigit(character) || character == '_');

    private static string? HeadOf(XElement root)
    {
        string? Value(string name) => root.Attribute(XName.Get("Page." + name, AiorosNamespace))?.Value;
        var (title, template, absolute) = (Value("Title"), Value("TitleTemplate"), Value("AbsoluteTitle"));
        var (description, canonical, image, type, indexable) = (Value("Description"), Value("Canonical"), Value("Image"), Value("Type"), Value("Indexable"));
        var metadata = description is null && canonical is null && image is null && type is null && indexable is null
            ? "null"
            : "new global::Aioros.Pages.PageMetadata(" + Literal(description) + ", " + Literal(canonical) + ", " + Literal(image) + ", " + Literal(type ?? "website") + ", "
                + (string.Equals(indexable?.Trim(), "false", StringComparison.OrdinalIgnoreCase) ? "false" : "true") + ")";
        return title is null && template is null && absolute is null && metadata == "null"
            ? null
            : "new global::Aioros.Pages.PageHead(" + Literal(absolute ?? title) + ", " + Literal(template) + ", " + (absolute is null ? "false" : "true") + ", " + metadata + ")";
    }

    private static string Literal(string? text) => text is null ? "null" : "@\"" + text.Replace("\"", "\"\"") + "\"";

    private static string? Resolve(XElement root, string? dataType)
    {
        if (dataType is null)
        {
            return null;
        }

        if (dataType.StartsWith("{x:Type ", StringComparison.Ordinal) && dataType.EndsWith("}", StringComparison.Ordinal))
        {
            dataType = dataType.Substring(8, dataType.Length - 9).Trim();
        }

        var colon = dataType.IndexOf(':');
        if (colon <= 0)
        {
            return null;
        }

        var space = Clr(root.GetNamespaceOfPrefix(dataType.Substring(0, colon))?.NamespaceName);
        return space is null ? null : space + "." + dataType.Substring(colon + 1);
    }

    private static string? Clr(string? space)
    {
        if (space is null)
        {
            return null;
        }

        if (space.StartsWith("using:", StringComparison.Ordinal))
        {
            return space.Substring(6);
        }

        if (space.StartsWith("clr-namespace:", StringComparison.Ordinal))
        {
            var clr = space.Substring(14);
            var semicolon = clr.IndexOf(';');
            return semicolon < 0 ? clr : clr.Substring(0, semicolon);
        }

        return null;
    }
}

/// <summary>An element that may place a view, with the list whose item template holds it or the run-time template around it.</summary>
internal sealed class XamlReference(string position, string? type, string? source, XamlList? list, string? runTime, (string? Path, string Binding)? itemKey, bool ownContext = false)
{
    public string Position { get; } = position;

    public string? Type { get; } = type;

    public string? Source { get; } = source;

    public XamlList? List { get; } = list;

    /// <summary>The template, style or resources that hold the element, whose content is chosen at run time.</summary>
    public string? RunTime { get; } = runTime;

    /// <summary>The element's <c>a:View.ItemKey</c>: its member path, when it is one, and the binding as written.</summary>
    public (string? Path, string Binding)? ItemKey { get; } = itemKey;

    /// <summary>Whether the element sets its own <c>DataContext</c>, which Avalonia gives the view instead of a view model of its own.</summary>
    public bool OwnContext { get; } = ownContext;
}

/// <summary>
/// A list whose item template places views: its ordinal and position among the host's lists with an item template, its
/// <c>ItemsSource</c> path, the item type its template declares, and whether the generator can follow it.
/// </summary>
internal sealed class XamlList(int ordinal, string position, string? items, bool nested, bool contextChanged, string? itemType = null, string? binding = null)
{
    public int Ordinal { get; } = ordinal;

    public string Position { get; } = position;

    public string? Items { get; } = items;

    public string? Binding { get; } = binding;

    public bool Nested { get; } = nested;

    public bool ContextChanged { get; } = contextChanged;

    public string? ItemType { get; } = itemType;

    public XamlList Typed(string? itemType) => new(Ordinal, Position, Items, Nested, ContextChanged, itemType ?? string.Empty, Binding);
}
