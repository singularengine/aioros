using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Lumen.Views;

public sealed partial class IssueCard : UserControl
{
    public IssueCard() => AvaloniaXamlLoader.Load(this);
}
