using Aioros.Avalonia.Hosting;
using Aioros.Platform;
using Aioros.Targets.Desktop.Handoff;
using Aioros.Targets.Desktop.Navigation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Rendering;
using Avalonia.Threading;

namespace Aioros.Targets.Desktop.Hosting;

public static class DesktopHost
{
    public const string HeadlessVariable = "AIOROS_DESKTOP_HEADLESS";
    public const string CaptureVariable = "AIOROS_DESKTOP_CAPTURE";

    /// <summary>Set to 1 to show Avalonia's frame rate and layout and render time graphs over the window.</summary>
    public const string FramesVariable = "AIOROS_DESKTOP_FPS";

    public static int Run(DesktopApp app, string[] args)
    {
        ArgumentNullException.ThrowIfNull(app);
        var headless = Environment.GetEnvironmentVariable(HeadlessVariable) == "1";
        var instance = app.Scheme is { } scheme && !headless ? SingleInstance.Claim(SingleInstance.NameFor(scheme), args) : null;
        if (app.Scheme is not null && !headless && instance is null)
        {
            return 0;
        }

        var builder = AppBuilder.Configure(() => (Application)Activator.CreateInstance(app.ApplicationType ?? typeof(DefaultApplication))!)
            .WithInterFont()
            .AfterSetup(started => Dispatcher.UIThread.Post(() => _ = OpenAsync(app, args, instance)));
        if (headless)
        {
            builder = AvaloniaHeadlessPlatformExtensions.UseHeadless(builder.UseSkia().UseHarfBuzz(), new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
        }
        else
        {
            builder = builder.UsePlatformProfile();
            if (Development)
            {
                Console.Error.WriteLine(PlatformProfile.Current.Describe());
            }
        }
        return builder.StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
    }

    private static bool Development =>
        (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")) == "Development";

    private static async Task OpenAsync(DesktopApp app, string[] args, SingleInstance? instance)
    {
        await app.StartAsync(CancellationToken.None);
        var window = new Window { Title = app.Assembly.GetName().Name, Width = 1280, Height = 800 };
        var sources = new XamlSources(app.Assembly, app.ProjectDirectory);
        var navigator = new DesktopNavigator(window, sources, app.Services, app.Pages, app.NotFound, app.Error);
        navigator.Failed += (_, exception) => Console.Error.WriteLine($"Navigation failed: {exception}");
        navigator.Browser = async address =>
        {
            await Console.Error.WriteLineAsync($"Sign in through the browser: {address}");
            _ = await window.Launcher.LaunchUriAsync(address);
        };
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.MainWindow = window;
            lifetime.Exit += (_, _) => _ = app.StopAsync(CancellationToken.None);
            lifetime.Exit += (_, _) => _ = instance?.DisposeAsync().AsTask();
            ShutDownWhenStopping(() => lifetime.Shutdown(), app.Stopping);
        }

        if (Environment.GetEnvironmentVariable(FramesVariable) == "1")
        {
            window.RendererDiagnostics.DebugOverlays = RendererDebugOverlays.Fps | RendererDebugOverlays.LayoutTimeGraph | RendererDebugOverlays.RenderTimeGraph;
        }

        var start = FirstRun.Path(
            app.Scheme is { } scheme ? Handoff(app, scheme, args, window, navigator, instance) : app.StartPath,
            app.FirstRun,
            (app.Services.GetService(typeof(Aioros.Runtime.SignIn.SignInClient)) as Aioros.Runtime.SignIn.SignInClient)?.Options);
        window.Show();
        await navigator.TryNavigateAsync(start);
        if (Development && app.ProjectDirectory is { } project && Directory.Exists(project))
        {
            var reload = new HotReload.DesktopHotReload(window, navigator, sources, project);
            reload.Reloaded += (_, reloaded) => Console.Error.WriteLine(reload.Fault ?? $"Hot reload: {reloaded.Source ?? "C# edit"} shown in {reloaded.Took.TotalMilliseconds:0} ms");
            _ = reload.RunAsync(app.Stopping);
        }
        if (Environment.GetEnvironmentVariable(CaptureVariable) is { Length: > 0 } capture)
        {
            await CaptureAsync(window, capture, app.Services.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System);
        }
    }

    /// <summary>
    /// Wires the native install handoff: deep links forwarded by later instances (Windows, Linux) or delivered by the
    /// system (macOS, through <see cref="IActivatableLifetime"/>), the scheme's registration and the updates of the
    /// installed app; answers the first path, a deep link's when the app was started with one.
    /// </summary>
    private static string Handoff(DesktopApp app, string scheme, string[] args, Window window, DesktopNavigator navigator, SingleInstance? instance)
    {
        var routes = DeepLink.Routes(app.Pages);
        async Task OpenAsync(string? path)
        {
            window.Activate();
            if (path is not null)
            {
                await navigator.TryNavigateAsync(path);
            }
        }

        if (instance is not null)
        {
            _ = DeepLink.FollowAsync(instance.Forwarded, scheme, routes, path => Dispatcher.UIThread.InvokeAsync(() => OpenAsync(path)), app.Stopping);
        }

        if (Application.Current?.TryGetFeature<IActivatableLifetime>() is { } activatable)
        {
            activatable.Activated += (sender, activated) =>
            {
                if (activated is ProtocolActivatedEventArgs { Kind: ActivationKind.OpenUri } opened)
                {
                    _ = OpenAsync(DeepLink.Path(opened.Uri.AbsoluteUri, scheme, routes));
                }
            };
        }

        if (app.Installed)
        {
            _ = Task.Run(() => SchemeRegistration.RegisterAsync(scheme, app.Assembly.GetName().Name ?? scheme, app.Stopping), app.Stopping);
            if (app.Updates is { } feed)
            {
                var updates = new AppUpdates(new Velopack.UpdateManager(feed), Console.Error, args);
                navigator.Outdated = updates.RestartOnAsync;
                _ = updates.DownloadOnStartAsync(app.Stopping);
            }
        }

        var (start, rejected) = DeepLink.Start(args, scheme, routes, app.StartPath);
        if (rejected is not null)
        {
            Console.Error.WriteLine(rejected);
        }

        return start;
    }

    /// <summary>
    /// Shuts the desktop lifetime down on the UI thread when the host stops. <c>SIGTERM</c> reaches the host's
    /// <c>ConsoleLifetime</c>, which cancels the signal's default termination and only stops the host, so without this the
    /// window's main loop keeps the process alive after "Application is shutting down...".
    /// </summary>
    public static CancellationTokenRegistration ShutDownWhenStopping(Action shutDown, CancellationToken stopping) =>
        stopping.Register(() => Dispatcher.UIThread.Post(shutDown));

    /// <summary>
    /// Saves the window's frame once the page has settled (<see cref="SettledFrames"/>): each frame comes a display frame
    /// after the last, on the app's clock, so its animations move on as they would on screen.
    /// </summary>
    private static async Task CaptureAsync(Window window, string path, TimeProvider time)
    {
        using var frame = await SettledFrames.WaitAsync(
            () => window.CaptureRenderedFrame() is { } bitmap ? new CapturedFrame(bitmap) : null,
            static (a, b) => a.Pixels.AsSpan().SequenceEqual(b.Pixels),
            static () => PendingAnimations.Active,
            async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(16), time);
                Dispatcher.UIThread.RunJobs();
            });
        frame?.Bitmap.Save(path, PngBitmapEncoderOptions.Default);
        window.Close();
    }

    /// <summary>A rendered frame and a copy of its pixels to compare.</summary>
    private sealed class CapturedFrame : IDisposable
    {
        public CapturedFrame(global::Avalonia.Media.Imaging.WriteableBitmap bitmap)
        {
            Bitmap = bitmap;
            using var locked = bitmap.Lock();
            Pixels = new byte[locked.RowBytes * locked.Size.Height];
            System.Runtime.InteropServices.Marshal.Copy(locked.Address, Pixels, 0, Pixels.Length);
        }

        public global::Avalonia.Media.Imaging.WriteableBitmap Bitmap { get; }

        public byte[] Pixels { get; }

        public void Dispose() => Bitmap.Dispose();
    }
}
