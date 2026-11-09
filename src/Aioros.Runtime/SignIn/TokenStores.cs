using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Aioros.Runtime.SignIn;

/// <summary>Where the installed app keeps its tokens between runs: the operating system's store for secrets.</summary>
public interface ITokenStore
{
    ValueTask<TokenSet?> ReadAsync(CancellationToken cancellationToken);

    ValueTask WriteAsync(TokenSet tokens, CancellationToken cancellationToken);

    ValueTask ClearAsync(CancellationToken cancellationToken);
}

/// <summary>Runs a command line tool with its arguments (never through a shell) and what it reads on standard input.</summary>
public delegate Task<CommandResult> RunCommand(string file, IReadOnlyList<string> arguments, string? input, CancellationToken cancellationToken);

public sealed record CommandResult(int ExitCode, string Output);

/// <summary>Picks the store of the operating system the app runs on.</summary>
public static partial class TokenStores
{
    /// <summary>
    /// DPAPI on Windows, the Keychain on macOS, libsecret on Linux when <c>secret-tool</c> is installed; otherwise the
    /// tokens are kept in memory for the session and a warning says so, never in a file in clear.
    /// </summary>
    public static ITokenStore ForCurrentOs(string app, Uri server, ILogger logger, Func<string, bool>? onPath = null)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(logger);
        var origin = server.GetLeftPart(UriPartial.Authority);
        if (OperatingSystem.IsWindows())
        {
            return new DpapiTokenStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aioros", app, Uri.EscapeDataString(origin) + ".tokens"));
        }

        if (OperatingSystem.IsMacOS())
        {
            return new KeychainTokenStore(app, origin);
        }

        if ((onPath ?? OnPath)("secret-tool"))
        {
            return new SecretToolTokenStore(app, origin);
        }

        NoStore(logger, app);
        return new SessionTokenStore();
    }

    /// <summary>Runs a tool and returns its exit code and standard output; a tool that cannot be started fails with 127.</summary>
    public static async Task<CommandResult> RunAsync(string file, IReadOnlyList<string> arguments, string? input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var start = new ProcessStartInfo(file) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        Process process;
        try
        {
            process = Process.Start(start) ?? throw new InvalidOperationException(file + " did not start.");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new CommandResult(127, string.Empty);
        }

        using (process)
        {
            await process.StandardInput.WriteAsync(input.AsMemory(), cancellationToken);
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            await errors;
            return new CommandResult(process.ExitCode, await output);
        }
    }

    private static bool OnPath(string tool) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Any(folder => File.Exists(Path.Combine(folder, tool)));

    [LoggerMessage(Level = LogLevel.Warning, Message = "No secure store for {App}'s sign-in was found (libsecret's secret-tool is not installed): the tokens are kept in memory and the app signs in again on its next start.")]
    private static partial void NoStore(ILogger logger, string app);
}

/// <summary>libsecret through <c>secret-tool</c>: the tokens go on standard input, never on the command line.</summary>
public sealed class SecretToolTokenStore(string app, string origin, RunCommand? run = null) : ITokenStore
{
    private readonly RunCommand _run = run ?? TokenStores.RunAsync;

    private string[] Attributes => ["service", "aioros", "app", app, "server", origin];

    public async ValueTask<TokenSet?> ReadAsync(CancellationToken cancellationToken)
    {
        var result = await _run("secret-tool", ["lookup", .. Attributes], null, cancellationToken);
        return result.ExitCode == 0 ? TokenSet.Read(result.Output) : null;
    }

    public async ValueTask WriteAsync(TokenSet tokens, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var result = await _run("secret-tool", ["store", $"--label={app} sign-in ({origin})", .. Attributes], tokens.Write(), cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"libsecret did not store the sign-in (secret-tool exited with {result.ExitCode}).");
        }
    }

    public async ValueTask ClearAsync(CancellationToken cancellationToken) => await _run("secret-tool", ["clear", .. Attributes], null, cancellationToken);
}

/// <summary>The macOS Keychain through <c>security</c>, as a generic password of the app for its server.</summary>
public sealed class KeychainTokenStore(string app, string origin, RunCommand? run = null) : ITokenStore
{
    private readonly RunCommand _run = run ?? TokenStores.RunAsync;

    private string Service => "aioros:" + app;

    public async ValueTask<TokenSet?> ReadAsync(CancellationToken cancellationToken)
    {
        var result = await _run("security", ["find-generic-password", "-s", Service, "-a", origin, "-w"], null, cancellationToken);
        return result.ExitCode == 0 ? TokenSet.Read(result.Output) : null;
    }

    public async ValueTask WriteAsync(TokenSet tokens, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var result = await _run("security", ["add-generic-password", "-U", "-s", Service, "-a", origin, "-w", tokens.Write()], null, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"The Keychain did not store the sign-in (security exited with {result.ExitCode}).");
        }
    }

    public async ValueTask ClearAsync(CancellationToken cancellationToken) => await _run("security", ["delete-generic-password", "-s", Service, "-a", origin], null, cancellationToken);
}

/// <summary>A file protected with DPAPI for the current Windows user.</summary>
public sealed class DpapiTokenStore(string file, Func<byte[], byte[]>? protect = null, Func<byte[], byte[]>? unprotect = null) : ITokenStore
{
    private static readonly byte[] Entropy = "Aioros sign-in"u8.ToArray();
    private readonly Func<byte[], byte[]> _protect = protect ?? Protect;
    private readonly Func<byte[], byte[]> _unprotect = unprotect ?? Unprotect;

    public async ValueTask<TokenSet?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            return TokenSet.Read(Encoding.UTF8.GetString(_unprotect(await File.ReadAllBytesAsync(file, cancellationToken))));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public async ValueTask WriteAsync(TokenSet tokens, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllBytesAsync(file, _protect(Encoding.UTF8.GetBytes(tokens.Write())), cancellationToken);
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken)
    {
        File.Delete(file);
        return ValueTask.CompletedTask;
    }

    private static byte[] Protect(byte[] data) => OperatingSystem.IsWindows() ? Windows.Seal(data) : throw new PlatformNotSupportedException("DPAPI exists only on Windows.");

    private static byte[] Unprotect(byte[] data) => OperatingSystem.IsWindows() ? Windows.Open(data) : throw new PlatformNotSupportedException("DPAPI exists only on Windows.");

    [SupportedOSPlatform("windows")]
    private static class Windows
    {
        public static byte[] Seal(byte[] data) => ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);

        public static byte[] Open(byte[] data) => ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
    }
}

/// <summary>Keeps the tokens in memory for this run only, where the system has no store; reported when chosen.</summary>
public sealed class SessionTokenStore : ITokenStore
{
    private TokenSet? _tokens;

    public ValueTask<TokenSet?> ReadAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Volatile.Read(ref _tokens));

    public ValueTask WriteAsync(TokenSet tokens, CancellationToken cancellationToken)
    {
        Volatile.Write(ref _tokens, tokens);
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken)
    {
        Volatile.Write(ref _tokens, null);
        return ValueTask.CompletedTask;
    }
}
