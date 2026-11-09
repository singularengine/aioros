using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Stylet.Avalonia;
using Stylet.Avalonia.Primitive;
using Stylet.Samples.HelloDialog;

namespace StyletSamples.Composition;

public static class StyletHosting
{
    public static IServiceCollection AddStylet(this IServiceCollection services, IReadOnlyDictionary<Type, Func<Control>> windows) => services
        .AddSingleton<IViewManager>(new WindowViews(windows))
        .AddSingleton<IWindowManagerConfig, ActiveWindow>()
        .AddSingleton<IWindowManager, WindowManager>()
        .AddSingleton<IEventAggregator, EventAggregator>()
        .AddTransient<IMessageBoxViewModel, MessageBoxViewModel>();

    public static void UseStylet(this IServiceProvider services)
    {
        IoC.GetInstance = (service, _) => services.GetRequiredService(service);
        IoC.GetInstances = service => services.GetServices(service).OfType<object>();
    }

    private sealed class WindowViews(IReadOnlyDictionary<Type, Func<Control>> windows)
        : ViewManager(new ViewManagerConfig { ViewFactory = _ => throw new InvalidOperationException(), ViewAssemblies = [] })
    {
        public override Control CreateViewForModel(object model) =>
            windows.TryGetValue(model.GetType(), out var create)
                ? create()
                : throw new StyletViewLocationException($"No window is mapped for {model.GetType().Name}.", model.GetType().Name);
    }

    public sealed class DialogFactory(IServiceProvider services) : IDialogFactory
    {
        public Dialog1ViewModel CreateDialog1() => services.GetRequiredService<Dialog1ViewModel>();
    }

    private sealed class ActiveWindow : IWindowManagerConfig
    {
        public TopLevel? GetActiveWindow() =>
            Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.Windows.FirstOrDefault(window => window.IsActive) ?? desktop.MainWindow
                : throw new NotSupportedException("Stylet's windows and dialogs are native only.");
    }
}
