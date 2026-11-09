#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Threading;

namespace AiorosCapture;

public static class Capture
{
    [STAThread]
    public static int Main(string[] args)
    {
        var output = Path.GetFullPath(args[0]);
        var width = int.Parse(args[1]);
        var height = int.Parse(args[2]);
        var settle = TimeSpan.FromMilliseconds(int.Parse(args[3]));
        var appArgs = args.Skip(4).ToArray();
        var lifetime = new ClassicDesktopStyleApplicationLifetime { Args = appArgs, ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Window? opened = null;
        var builder = Target.Build(appArgs);
        Window.WindowOpenedEvent.AddClassHandler(typeof(Window), (sender, _) => opened ??= sender as Window);
        builder
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithLifetime(lifetime);
        var window = lifetime.MainWindow ?? lifetime.Windows.FirstOrDefault() ?? opened ?? throw new InvalidOperationException("The app opened no window.");
        window.Show();
        Pump();
        window.SizeToContent = SizeToContent.Manual;
        window.WindowState = WindowState.Normal;
        window.Width = width;
        window.Height = height;
        var until = DateTime.UtcNow + settle;
        while (DateTime.UtcNow < until)
        {
            Pump();
            Thread.Sleep(25);
        }
        Pump();
        var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame was rendered.");
        frame.Save(output);
        Console.WriteLine(output + " " + frame.PixelSize);
        return 0;
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
