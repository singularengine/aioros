using Avalonia;
using Avalonia.Markup.Xaml;

namespace ReactiveUI.Samples.Suspension
{
    public class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);
    }
}
