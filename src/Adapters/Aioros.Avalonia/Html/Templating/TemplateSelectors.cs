using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Aioros.Runtime.Rendering;
using Aioros.Runtime.Slots;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml.Templates;

namespace Aioros.Avalonia.Html.Templating;

/// <summary>A template an app's template selector holds, with the entry the plans file names it by.</summary>
internal sealed record HeldTemplate(SelectorEntry Entry, IDataTemplate Template);

/// <summary>
/// An app's template selector (its own <c>IDataTemplate</c>, Avalonia's <c>DataTemplateSelector</c> pattern): the
/// templates it holds in <c>IDataTemplate</c> properties and in dictionaries of templates by text, enum, number or type,
/// and a chooser that rebuilds it with a <see cref="TemplateMark"/> in place of each, so its own <c>Match</c> and
/// <c>Build</c> name the template an item takes without building a control. This is the development engine's way, by
/// reflection when the plan is compiled; the app's generated code does the same in production.
/// </summary>
internal static class TemplateSelectors
{
    /// <summary>Whether a template is an app's selector rather than one of Avalonia's own templates or this adapter's repeated views.</summary>
    public static bool IsSelector(IDataTemplate? template) =>
        template is not null && template is not DataTemplate && template.GetType().Assembly != typeof(TemplateSelectors).Assembly
        && !(template.GetType().Assembly.GetName().Name ?? string.Empty).StartsWith("Avalonia", StringComparison.Ordinal);

    /// <summary>The templates the selector holds, in the order of its members, or why a plan cannot rebuild it.</summary>
    public static (ImmutableArray<HeldTemplate> Held, string? Failure) Held(IDataTemplate selector)
    {
        var type = selector.GetType();
        var held = ImmutableArray.CreateBuilder<HeldTemplate>();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.GetIndexParameters().Length == 0))
        {
            if (Member(selector, property, held) is { } failure)
            {
                return ([], $"the template selector {type.Name} {failure}");
            }
        }

        if (held.Count > 0 && type.GetConstructor(Type.EmptyTypes) is null)
        {
            return ([], $"the template selector {type.Name} has no public parameterless constructor to rebuild it with");
        }

        return (held.ToImmutable(), null);
    }

    private static string? Member(IDataTemplate selector, PropertyInfo property, ImmutableArray<HeldTemplate>.Builder held)
    {
        var writable = property.SetMethod is { IsPublic: true };
        if (typeof(IDataTemplate).IsAssignableFrom(property.PropertyType) && writable)
        {
            if (property.GetValue(selector) is IDataTemplate template)
            {
                held.Add(new HeldTemplate(new SelectorEntry(property.Name, string.Empty), template));
            }

            return null;
        }

        if (KeyType(property.PropertyType) is { } keyType && property.GetValue(selector) is IDictionary templates)
        {
            if (!Keyed(keyType))
            {
                return $"keys its templates by {keyType.Name}, which a plan cannot name (text, an enum, a number or a type)";
            }

            held.AddRange(Entries(templates).Where(entry => entry.Value is IDataTemplate)
                .Select(entry => new HeldTemplate(new SelectorEntry(property.Name, Key(entry.Key)), (IDataTemplate)entry.Value!)));
            return null;
        }

        return writable ? $"has a setting ({property.Name}) a plan cannot rebuild" : null;
    }

    /// <summary>The slot that names the selector and its templates in the plans file.</summary>
    public static SlotSpec Spec(IDataTemplate selector, ImmutableArray<HeldTemplate> held) =>
        PlanSelectors.Spec(selector.GetType().AssemblyQualifiedName!, held.Select(item => item.Entry));

    /// <summary>The fallback's chooser for a selector slot, rebuilt by reflection, as a getter of the chosen index.</summary>
    public static ValueGetter Getter(SlotSpec spec)
    {
        var type = Type.GetType(spec.Argument!, throwOnError: true)!;
        var choose = Chooser(type, PlanSelectors.Entries(spec)) ?? throw new InvalidOperationException($"The template selector {type.Name} cannot be rebuilt with its templates.");
        return item => choose(item);
    }

    /// <summary>A new selector of the type with a mark for each entry, and the index its <c>Build</c> picks for an item; nothing when an entry names no member it has.</summary>
    public static Func<object?, int>? Chooser(Type type, ImmutableArray<SelectorEntry> entries)
    {
        if (Activator.CreateInstance(type) is not IDataTemplate selector)
        {
            return null;
        }

        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var property = type.GetProperty(entry.Member, BindingFlags.Public | BindingFlags.Instance);
            if (property is null)
            {
                return null;
            }

            if (typeof(IDataTemplate).IsAssignableFrom(property.PropertyType))
            {
                property.SetValue(selector, new TemplateMark(index));
            }
            else if (KeyType(property.PropertyType) is { } keyType && property.GetValue(selector) is IDictionary templates)
            {
                templates[Parse(keyType, entry.Key)] = new TemplateMark(index);
            }
            else
            {
                return null;
            }
        }

        return item => TemplateMark.Choose(selector, item);
    }

    /// <summary>An item a held template can be learned from when none of the page's items takes it: a stand-in of its data type.</summary>
    public static object? StandIn(IDataTemplate template) =>
        template is DataTemplate { DataType: { IsAbstract: false, IsInterface: false } type }
            ? type == typeof(string) ? string.Empty : RuntimeHelpers.GetUninitializedObject(type)
            : null;

    public static string Missing(IDataTemplate selector, SelectorEntry entry) =>
        $"the template selector {selector.GetType().Name} holds a template ({entry.Member}{(entry.Key.Length == 0 ? string.Empty : "[" + entry.Key + "]")}) that no item takes and that names no data type to learn it from";

    private static IEnumerable<DictionaryEntry> Entries(IDictionary templates)
    {
        foreach (DictionaryEntry entry in templates)
        {
            yield return entry;
        }
    }

    private static Type? KeyType(Type type) =>
        type.GetInterfaces().Append(type)
            .FirstOrDefault(face => face.IsGenericType && face.GetGenericTypeDefinition() == typeof(IDictionary<,>) && typeof(IDataTemplate).IsAssignableFrom(face.GetGenericArguments()[1]))?
            .GetGenericArguments()[0];

    private static bool Keyed(Type type) => type == typeof(string) || type == typeof(int) || type == typeof(Type) || type.IsEnum;

    private static string Key(object key) => key switch
    {
        Type type => type.AssemblyQualifiedName!,
        IFormattable formattable when key is not Enum => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => key.ToString() ?? string.Empty,
    };

    private static object Parse(Type type, string key) =>
        type == typeof(string) ? key
        : type == typeof(int) ? int.Parse(key, CultureInfo.InvariantCulture)
        : type == typeof(Type) ? Type.GetType(key, throwOnError: true)!
        : Enum.Parse(type, key);
}

/// <summary>
/// Stands in a rebuilt selector for one of its templates: building it records which one the selector picked, on the
/// rendering thread, and builds no control.
/// </summary>
internal sealed class TemplateMark(int index) : IDataTemplate
{
    [ThreadStatic]
    private static int _chosen;

    public static int Choose(IDataTemplate selector, object? item)
    {
        if (!selector.Match(item))
        {
            return -1;
        }

        _chosen = -1;
        selector.Build(item);
        return _chosen;
    }

    public Control? Build(object? param)
    {
        Record(index);
        return null;
    }

    private static void Record(int chosen) => _chosen = chosen;

    public bool Match(object? data) => true;
}
