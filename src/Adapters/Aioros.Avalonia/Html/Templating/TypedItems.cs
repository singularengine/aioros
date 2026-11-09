using System.Collections.Immutable;
using Aioros.Avalonia.Plans;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Templates;

namespace Aioros.Avalonia.Html.Templating;

/// <summary>
/// Lists whose items take their template by type: the typed <c>DataTemplate</c>s in the order Avalonia looks for them
/// (the item template, then the control's and its ancestors' <c>DataTemplates</c>, then the application's), each
/// learned from the list's first item of its type, or from a stand-in when the list has none.
/// </summary>
internal static class TypedItems
{
    public static List<DataTemplate> Templates(ItemsControl items) => Templates(items, items.ItemTemplate);

    /// <summary>The typed templates a control's data can take: its own template first, then the hosts' in Avalonia's order.</summary>
    public static List<DataTemplate> Templates(Control control, IDataTemplate? own)
    {
        var found = new List<DataTemplate>();
        var hosts = control.GetLogicalAncestors().Prepend(control).OfType<IDataTemplateHost>()
            .Where(host => host.IsDataTemplatesInitialized).SelectMany(host => host.DataTemplates);
        var candidates = (own is { } template ? hosts.Prepend(template) : hosts)
            .Concat(Application.Current?.DataTemplates ?? []);
        foreach (var candidate in candidates)
        {
            if (candidate is not DataTemplate data || found.Any(known => known.DataType == data.DataType))
            {
                continue;
            }

            found.Add(data);
            if (data.DataType is null)
            {
                break;
            }
        }

        return found;
    }

    private static object? Sample(ItemsControl items, Type type) =>
        items.ItemsSource?.Cast<object?>().FirstOrDefault(type.IsInstanceOfType);

    /// <summary>Writes the list as one repeat whose item picks its case by type.</summary>
    public static void Write(PlanRecorder plan, ItemsControl items, Panel panel, string path, List<DataTemplate> templates, Func<Control, ImmutableArray<IPlanNode>> write)
    {
        var cases = ImmutableArray.CreateBuilder<TypeCase>();
        foreach (var template in templates)
        {
            var type = template.DataType ?? typeof(object);
            using var standIn = Sample(items, type) is { } sample ? StandIns.Fill(items, sample) : StandIns.Fill(items, type);
            if (standIn is null || panel.Children.FirstOrDefault() is not Control learned)
            {
                plan.Fail($"a list template for {type.Name} that cannot be learned from a stand-in");
                return;
            }

            cases.Add(new TypeCase(type, write(learned)));
        }

        plan.Add(ItemIdentity.Keyed(plan, new RepeatNode(PropertyPaths.Getter(path), [new TypeSwitchNode(cases.ToImmutable())]) { Source = SlotSpec.Bound(path) }, ItemIdentity.Path(items, null)));
    }
}
