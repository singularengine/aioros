using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace Aioros.Avalonia.Tests.Models;

/// <summary>A library's attached state on a list it does not own, as Aioros.Cosmo's <c>c:DataTable.State</c> on a <c>TableView</c>: the page the list shows.</summary>
public static class Pager
{
    public static readonly AttachedProperty<int> PageProperty = AvaloniaProperty.RegisterAttached<ItemsControl, int>("Page", typeof(Pager), defaultBindingMode: BindingMode.TwoWay);

    public static int GetPage(ItemsControl list) => list.GetValue(PageProperty);

    public static void SetPage(ItemsControl list, int value) => list.SetValue(PageProperty, value);
}

/// <summary>A row whose text is edited in its own field.</summary>
public sealed class PagedRow(string text)
{
    public string Text { get; set; } = text;
}

/// <summary>A paged list: one row on the first page, two on the second, then a tail field after the list.</summary>
public sealed class PagedModel : INotifyPropertyChanged
{
    private readonly PagedRow[][] _pages = [[new("a")], [new("b"), new("c")]];

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Page
    {
        get;
        set
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Page)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Rows)));
        }
    }

    public IReadOnlyList<PagedRow> Rows => _pages[Page];

    public string Tail { get; set; } = "tail";
}
