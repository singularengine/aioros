using Avalonia;
using Avalonia.Themes.Fluent;

namespace Aioros.Avalonia.Hosting;

public sealed class DefaultApplication : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}
