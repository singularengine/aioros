using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace Aioros.Cli.Commands;

/// <summary>
/// Runs <c>dotnet watch</c> with its output echoed line by line and read for the build's errors, which are posted to the
/// app's development channel with the tool's token so the page shows them (Next.js's dev overlay).
/// </summary>
public static class WatchedRun
{
    public const string BuildPath = "/_aioros/dev/build";

    public const string TokenHeader = "X-Aioros-Development-Token";

    public static async Task<int> RunAsync(ProcessStartInfo start, Uri app, string token)
    {
        ArgumentNullException.ThrowIfNull(start);
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        if (!Console.IsOutputRedirected)
        {
            start.Environment["DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION"] = "1";
        }

        start.Environment[Planner.DevelopmentToolVariable] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        var lines = Channel.CreateUnbounded<(string Line, bool Error)>(new UnboundedChannelOptions { SingleReader = true });
        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, line) => Write(lines, line.Data, false);
        process.ErrorDataReceived += (_, line) => Write(lines, line.Data, true);
        process.Start();
        using var stop = new ToolStop(process);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var reading = ReadAsync(lines.Reader, new Uri(app, BuildPath), token);
        await process.WaitForExitAsync();
        lines.Writer.TryComplete();
        await reading;
        return process.ExitCode;
    }

    private static void Write(Channel<(string Line, bool Error)> lines, string? line, bool error)
    {
        if (line is not null)
        {
            lines.Writer.TryWrite((line, error));
        }
    }

    private static async Task ReadAsync(ChannelReader<(string Line, bool Error)> lines, Uri build, string token)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var output = new BuildOutput();
        await foreach (var (line, error) in lines.ReadAllAsync())
        {
            await (error ? Console.Error : Console.Out).WriteLineAsync(line);
            if (output.Read(line) is not { } errors)
            {
                continue;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, build) { Content = JsonContent.Create(new BuildReport(errors), BuildReportContext.Default.BuildReport) };
            request.Headers.Add(TokenHeader, token);
            try
            {
                using var _ = await client.SendAsync(request);
            }
            catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
            {
                continue;
            }
        }
    }
}

public sealed record BuildReport(ImmutableArray<BuildError> Errors);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BuildReport))]
public sealed partial class BuildReportContext : JsonSerializerContext;
