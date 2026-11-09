using Aioros.Ui;
using Avalonia;

namespace Aioros.Avalonia.Trees;

public sealed class AvaloniaUiTree(Visual root) : IUiTree
{
    public static string EngineName => "Avalonia";

    public string Engine => EngineName;

    public Visual Root => root;
}
