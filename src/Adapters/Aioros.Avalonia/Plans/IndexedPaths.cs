using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Aioros.Runtime.Slots;
using Avalonia.Data;
using Avalonia.Data.Core;

namespace Aioros.Avalonia.Plans;

/// <summary>
/// The integer indexes a binding reads lists through (<c>Items[1].Name</c>), as a plan's path writes them. A reflection
/// binding describes them as written; a compiled binding describes an indexer by its property alone (<c>Items.Item</c>)
/// and keeps its arguments in the getter Avalonia's XAML compiler generates for it, named after them
/// (<c>IList`1.Item[1]!Getter</c>), or in the accessor factory it gives a list that notifies its changes. A path through an
/// indexer that is not an integer index of a list (a dictionary's key) is not one a plan follows.
/// </summary>
internal static partial class IndexedPaths
{
    private static ImmutableDictionary<(Type Type, string Name), FieldInfo?> _fields = ImmutableDictionary<(Type Type, string Name), FieldInfo?>.Empty;

    /// <summary>
    /// The path with the indexes of the lists it reads, from the scope of this type; the path as it is when it reads no
    /// indexer or is not a member path; null when it reads through an indexer a plan cannot follow.
    /// </summary>
    public static string? Restore(string path, Type? scope, BindingExpressionBase binding)
    {
        if (path.Length == 0 || path[0] is '$' or '#' or '!' or '^' || path.Contains('(', StringComparison.Ordinal) || (!path.Contains('[', StringComparison.Ordinal) && !Reads(path, scope)))
        {
            return path;
        }

        var keys = new Lazy<Queue<string>>(() => new(Keys(binding)));
        var type = scope;
        var written = new StringBuilder();
        foreach (var segment in path.Split('.'))
        {
            if (!Segment(segment, ref type, written, keys))
            {
                return null;
            }
        }

        return written.ToString();
    }

    /// <summary>Writes one segment of a path with its indexes, moving to the type it reads; false for an indexer a plan cannot follow.</summary>
    private static bool Segment(string segment, ref Type? type, StringBuilder written, Lazy<Queue<string>> keys)
    {
        var bracket = segment.IndexOf('[', StringComparison.Ordinal);
        var name = bracket < 0 ? segment : segment[..bracket];
        var member = name.TrimEnd('^');
        if (Indexer(type, member) is { } indexer)
        {
            if (!keys.Value.TryDequeue(out var key) || !Index(key, type))
            {
                return false;
            }

            written.Append('[').Append(key).Append(']');
            type = indexer.PropertyType;
        }
        else if (name.Length > 0)
        {
            written.Append(written.Length > 0 ? "." : string.Empty).Append(name);
            type = MemberType(type, member);
        }

        for (var at = bracket; at >= 0; at = segment.IndexOf('[', at + 1))
        {
            var key = segment[(at + 1)..segment.IndexOf(']', at)];
            if (!Index(key, type))
            {
                return false;
            }

            written.Append('[').Append(key).Append(']');
            type = Element(type);
        }

        return true;
    }

    /// <summary>Whether a compiled path names an indexer by its property (<c>Items.Item</c>).</summary>
    private static bool Reads(string path, Type? scope)
    {
        var type = scope;
        foreach (var member in path.Split('.').Select(part => part.TrimEnd('^')))
        {
            if (Indexer(type, member) is not null)
            {
                return true;
            }

            type = MemberType(type, member);
        }

        return false;
    }

    /// <summary>An integer index of a list, or of a value whose type the plan cannot tell.</summary>
    private static bool Index(string key, Type? type) =>
        int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out _)
        && (type is null || type == typeof(object) || typeof(IList).IsAssignableFrom(type) || Generic(type, typeof(IReadOnlyList<>)) is not null || Generic(type, typeof(IList<>)) is not null);

    private static PropertyInfo? Indexer(Type? type, string member) =>
        type is null || member != "Item" || MemberType(type, member) is not null
            ? null
            : Properties(type).FirstOrDefault(property => property.Name == member && property.GetIndexParameters().Length == 1);

    private static Type? MemberType(Type? type, string member) =>
        type is null ? null : Properties(type).FirstOrDefault(property => property.Name == member && property.GetIndexParameters().Length == 0)?.PropertyType ?? type.GetField(member)?.FieldType;

    /// <summary>The public instance properties of a type, those of the interfaces it extends with an interface's own.</summary>
    private static IEnumerable<PropertyInfo> Properties(Type type) =>
        type.IsInterface ? type.GetProperties().Concat(type.GetInterfaces().SelectMany(face => face.GetProperties())) : type.GetProperties();

    private static Type? Element(Type? type) =>
        type is null ? null
        : type.IsArray ? type.GetElementType()
        : (Generic(type, typeof(IReadOnlyList<>)) ?? Generic(type, typeof(IList<>)))?.GenericTypeArguments[0];

    private static Type? Generic(Type type, Type definition) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == definition ? type : type.GetInterfaces().FirstOrDefault(face => face.IsGenericType && face.GetGenericTypeDefinition() == definition);

    /// <summary>The arguments of a compiled binding's indexers, in the order its path reads them.</summary>
    private static IEnumerable<string> Keys(BindingExpressionBase binding)
    {
        if (Field(binding.GetType(), "_nodes")?.GetValue(binding) is not IList nodes)
        {
            yield break;
        }

        foreach (var node in nodes)
        {
            var plugin = node is null ? null : Field(node.GetType(), "_plugin")?.GetValue(node);
            if (plugin is null || Field(plugin.GetType(), "_propertyInfo")?.GetValue(plugin) is not ClrPropertyInfo { Name: "Item" } info)
            {
                continue;
            }

            if (Field(typeof(ClrPropertyInfo), "_getter")?.GetValue(info) is Delegate getter && Argument().Match(getter.Method.Name) is { Success: true } named)
            {
                yield return named.Groups["key"].Value;
            }
            else if (Field(plugin.GetType(), "_accessorFactory")?.GetValue(plugin) is Delegate { Target: { } closure } && Field(closure.GetType(), "_index")?.GetValue(closure) is int index)
            {
                yield return index.ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    private static FieldInfo? Field(Type type, string name) =>
        ImmutableInterlocked.GetOrAdd(ref _fields, (type, name), static ((Type Type, string Name) key) => key.Type.GetRuntimeFields().FirstOrDefault(field => field.Name == key.Name));

    [GeneratedRegex(@"\[(?<key>[^\]]*)\](?:_[^!]*)?!Getter$")]
    private static partial Regex Argument();
}
