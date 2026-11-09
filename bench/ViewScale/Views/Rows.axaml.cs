using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ViewScale.Data;

namespace ViewScale.Views;

public sealed partial class Rows : UserControl
{
    public Rows() => AvaloniaXamlLoader.Load(this);
}

public sealed record RowsData(int Offset, Row[] Items);

public sealed class RowsViewModel(RowsData data)
{
    public string Heading { get; } = $"Rows from {data.Offset + 1}";
    public Row[] Items { get; } = data.Items;
}
