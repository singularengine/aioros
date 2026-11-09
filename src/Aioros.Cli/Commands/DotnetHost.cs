using System.Runtime.InteropServices;

namespace Aioros.Cli.Commands;

public static class DotnetHost
{
    public static string Path { get; } = Find();

    private static string Find()
    {
        var executable = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        var fromHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrEmpty(fromHost) && File.Exists(fromHost))
        {
            return fromHost;
        }

        var runtime = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory());
        return System.IO.Path.Combine(runtime.Parent!.Parent!.Parent!.FullName, executable);
    }
}
