using Avalonia.Controls;
using Avalonia.Input;

namespace Aioros.Avalonia.Tests.Models;

public sealed class Splitting : Aioros.Pages.IPageModel
{
    public string Title => "Splitting";

    public double Seen { get; set; }

    public int Completed { get; set; }

    public double Stars { get; set; }
}

public sealed class SyncedSplitter : GridSplitter
{
    protected override Type StyleKeyOverride => typeof(GridSplitter);

    protected override void OnDragCompleted(VectorEventArgs e)
    {
        base.OnDragCompleted(e);
        if (DataContext is Splitting model && Parent is Grid grid)
        {
            model.Seen = grid.ColumnDefinitions[0].ActualWidth;
            model.Stars = grid.Bounds.Width - Bounds.Width;
            model.Completed++;
        }
    }
}
