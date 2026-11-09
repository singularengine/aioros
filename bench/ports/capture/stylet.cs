using Avalonia;

namespace AiorosCapture;

internal static class Target
{
    public static AppBuilder Build(string[] args) => Stylet.Samples.Hello.Program.BuildAvaloniaApp();
}
