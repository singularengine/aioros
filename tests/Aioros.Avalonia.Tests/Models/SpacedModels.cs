using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Aioros.Avalonia.Tests.Models;

/// <summary>A grid that stacks its children with a spacing row before each but the first, its height bound to that child's visibility, as Aioros.Cosmo's field spaces its parts.</summary>
public sealed class SpacedRows : Grid
{
    public SpacedRows()
    {
        Children.CollectionChanged += (_, _) => Build();
    }

    private void Build()
    {
        RowDefinitions.Clear();
        for (var index = 0; index < Children.Count; index++)
        {
            if (index > 0)
            {
                var spacing = new RowDefinition(GridLength.Auto);
                spacing.Bind(RowDefinition.HeightProperty, new Binding(nameof(IsVisible)) { Source = Children[index], Converter = Spacings.Instance, ConverterParameter = "12" });
                RowDefinitions.Add(spacing);
            }

            RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SetRow(Children[index], RowDefinitions.Count - 1);
        }
    }
}

/// <summary>A spacing row's height: the parameter's pixels while the part it comes before is shown, none while it is hidden.</summary>
public sealed class Spacings : IValueConverter
{
    public static readonly Spacings Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? new GridLength(double.Parse(parameter as string ?? "0", CultureInfo.InvariantCulture)) : new GridLength(0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
