using System.Collections.Concurrent;
using System.Text;
using Aioros.Runtime.SignIn;
using Microsoft.Extensions.Logging;

namespace Aioros.Runtime.Tests.SignIn;

public sealed class TokenStoreTests
{
    private static readonly TokenSet Tokens = new("access", "refresh", new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    [Trait("AC", "SIGNIN-09")]
    public async Task EachStoreKeepsTheTokensInItsSystemsFormAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var secretTool = new Commands { Output = Tokens.Write() };
        var libsecret = new SecretToolTokenStore("Shop", "https://shop.example", secretTool.RunAsync);
        await libsecret.WriteAsync(Tokens, token);
        var read = await libsecret.ReadAsync(token);
        await libsecret.ClearAsync(token);
        secretTool.Exit = 1;
        var missing = await libsecret.ReadAsync(token);

        string[] attributes = ["service", "aioros", "app", "Shop", "server", "https://shop.example"];
        Assert.Equal(
            [
                ("secret-tool", string.Join(' ', ["store", "--label=Shop sign-in (https://shop.example)", .. attributes]), Tokens.Write()),
                ("secret-tool", string.Join(' ', ["lookup", .. attributes]), null),
                ("secret-tool", string.Join(' ', ["clear", .. attributes]), null),
                ("secret-tool", string.Join(' ', ["lookup", .. attributes]), null),
            ],
            secretTool.Runs);
        Assert.All(secretTool.Runs, run => Assert.DoesNotContain("refresh", run.Arguments, StringComparison.Ordinal));
        Assert.Equal(Tokens, read);
        Assert.Null(missing);

        var security = new Commands { Output = Tokens.Write() + "\n" };
        var keychain = new KeychainTokenStore("Shop", "https://shop.example", security.RunAsync);
        await keychain.WriteAsync(Tokens, token);
        var fromKeychain = await keychain.ReadAsync(token);
        await keychain.ClearAsync(token);

        Assert.Equal(
            [
                ("security", "add-generic-password -U -s aioros:Shop -a https://shop.example -w " + Tokens.Write(), null),
                ("security", "find-generic-password -s aioros:Shop -a https://shop.example -w", null),
                ("security", "delete-generic-password -s aioros:Shop -a https://shop.example", null),
            ],
            security.Runs);
        Assert.Equal(Tokens, fromKeychain);

        var file = Path.Combine(Path.GetTempPath(), "aioros-tests", Guid.NewGuid().ToString("N"), "shop.tokens");
        var dpapi = new DpapiTokenStore(file, static data => [.. data.Select(static value => (byte)(value ^ 0x5A))], static data => [.. data.Select(static value => (byte)(value ^ 0x5A))]);
        await dpapi.WriteAsync(Tokens, token);
        var written = await File.ReadAllBytesAsync(file, token);
        var fromFile = await dpapi.ReadAsync(token);
        await dpapi.ClearAsync(token);

        Assert.DoesNotContain("refresh", Encoding.UTF8.GetString(written), StringComparison.Ordinal);
        Assert.Equal(Tokens, fromFile);
        Assert.False(File.Exists(file));
        Assert.Null(await dpapi.ReadAsync(token));
    }

    [Fact]
    [Trait("AC", "SIGNIN-10")]
    public async Task LibsecretKeepsTheTokensOnThisMachineAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var available = OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS") is { Length: > 0 }
            && (await TokenStores.RunAsync("secret-tool", ["--version"], null, token)).ExitCode != 127;
        Assert.SkipUnless(available, "libsecret's secret-tool or a session bus is not available here.");
        var store = new SecretToolTokenStore("Aioros.Tests." + Guid.NewGuid().ToString("N"), "https://tests.example");

        await store.WriteAsync(Tokens, token);
        var read = await store.ReadAsync(token);
        await store.ClearAsync(token);

        Assert.Equal(Tokens, read);
        Assert.Null(await store.ReadAsync(token));
    }

    [Fact]
    [Trait("AC", "SIGNIN-11")]
    public void TheStoreIsTheOperatingSystemsOrAReportedSessionStore()
    {
        var logs = new Logs();
        var withTool = TokenStores.ForCurrentOs("Shop", new Uri("https://shop.example/x"), logs, static tool => tool == "secret-tool");
        var withoutTool = TokenStores.ForCurrentOs("Shop", new Uri("https://shop.example/x"), logs, static _ => false);

        if (OperatingSystem.IsWindows())
        {
            Assert.IsType<DpapiTokenStore>(withTool);
            Assert.IsType<DpapiTokenStore>(withoutTool);
            Assert.Empty(logs.Warnings);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.IsType<KeychainTokenStore>(withTool);
            Assert.IsType<KeychainTokenStore>(withoutTool);
            Assert.Empty(logs.Warnings);
        }
        else
        {
            Assert.IsType<SecretToolTokenStore>(withTool);
            Assert.IsType<SessionTokenStore>(withoutTool);
            Assert.Contains("kept in memory", Assert.Single(logs.Warnings), StringComparison.Ordinal);
        }
    }

    private sealed class Commands
    {
        public ConcurrentQueue<(string File, string Arguments, string? Input)> Runs { get; } = new();

        public string Output { get; init; } = string.Empty;

        public int Exit { get; set; }

        public Task<CommandResult> RunAsync(string file, IReadOnlyList<string> arguments, string? input, CancellationToken cancellationToken)
        {
            Runs.Enqueue((file, string.Join(' ', arguments), input));
            return cancellationToken.IsCancellationRequested ? Task.FromCanceled<CommandResult>(cancellationToken) : Task.FromResult(new CommandResult(Exit, Output));
        }
    }

    private sealed class Logs : ILogger
    {
        public ConcurrentQueue<string> Warnings { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Enqueue(formatter(state, exception));
            }
        }
    }
}
