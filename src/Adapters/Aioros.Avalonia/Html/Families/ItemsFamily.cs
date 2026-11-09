using System.Collections.Immutable;
using Aioros.Avalonia.Forms;
using Aioros.Avalonia.Html.Writers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Aioros.Avalonia.Html.Families;

internal static class ItemsFamily
{
    public static ImmutableArray<IControlWriter> Writers { get; } =
    [
        new ControlWriter<ComboBox>(InputWriters.WriteSelect) { Rooted = true },
        new ControlWriter<TableView>(CollectionWriters.WriteTableView),
        new ControlWriter<DataGrid>(DataGridWriters.WriteDataGrid),
        new ControlWriter<ListBox>(ListWriters.WriteListBox),
        new ControlWriter<TabStrip>(ListWriters.WriteTabStrip),
        new ControlWriter<TabControl>(ListWriters.WriteTabControl),
        new ControlWriter<TreeView>(TreeWriters.WriteTreeView),
        new ControlWriter<Carousel>(CarouselWriters.Write),
        new ControlWriter<PipsPager>(PagerWriters.WritePipsPager),
    ];

    public static ImmutableArray<IFieldBinder> Binders { get; } =
    [
        new ChoiceBinder(),
        new FieldBinder<ComboBox>(BindSelection),
        new FieldBinder<SelectingItemsControl>(BindChoice),
        new FieldBinder<PipsPager>(BindPage),
        new StepBinder(),
    ];

    private static void BindSelection(ComboBox combo, string? value)
    {
        if (ItemParts.Indices(value) is [var index, ..] && index < combo.ItemCount)
        {
            combo.SelectedItem = combo.Items[index];
        }
    }

    private static void BindChoice(SelectingItemsControl list, string? value)
    {
        var indices = ItemParts.Indices(value).Where(index => index < list.ItemCount).ToList();
        if (list is ListBox box && box.SelectionMode.HasFlag(SelectionMode.Multiple))
        {
            box.Selection.Clear();
            foreach (var index in indices)
            {
                box.Selection.Select(index);
            }

            return;
        }

        if (indices.Count > 0)
        {
            list.SelectedIndex = indices[0];
        }
    }

    private static void BindPage(PipsPager pager, string? value)
    {
        var indices = ItemParts.Indices(value);
        if (indices.Count > 0 && indices[0] < pager.NumberOfPages)
        {
            pager.SetCurrentValue(PipsPager.SelectedPageIndexProperty, indices[0]);
        }
    }

    private sealed class ChoiceBinder : IFieldBinder
    {
        public bool Handles(Control control) => ChoiceLists.Property(control) is not null;

        public void Bind(Control control, string? value) => ChoiceLists.Bind(control, value);
    }

    private sealed class StepBinder : IFieldBinder
    {
        public bool Handles(Control control) => PagerWriters.IsStep(control);

        public void Bind(Control control, string? value) => PagerWriters.Step((Button)control, value);
    }
}
