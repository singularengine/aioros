using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Aioros.Runtime.Events;
using Aioros.Targets.Ssr.Development;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting.Internal;

namespace Aioros.Targets.Ssr.Tests.Development;

public sealed class DevelopmentBuildTests
{
    private const string Token = "0123456789abcdef";

    [Fact]
    [Trait("AC", "SSR-289")]
    public async Task TheBuildsErrorsReachThePageAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = Path.GetDirectoryName(Here())!;
        var environment = new HostingEnvironment { ContentRootPath = root };
        var bus = new EventBus();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await using var reported = bus.SubscribeAsync<BuildReported>(stop.Token).GetAsyncEnumerator(stop.Token);
        var options = new SsrOptions { Development = true, DevelopmentToken = Token };
        var source = Here();
        var failing = $$"""{"errors":[{"file":"{{JsonEncodedText.Encode(source)}}","line":20,"column":9,"code":"CS1002","message":"; expected"}]}""";

        var failed = await PostAsync(bus, options, environment, failing, Token, IPAddress.Loopback);
        await reported.MoveNextAsync();
        var errors = reported.Current;
        var cleared = await PostAsync(bus, options, environment, """{"errors":[]}""", Token, IPAddress.IPv6Loopback);
        await reported.MoveNextAsync();
        var fixedBuild = reported.Current;
        int[] refused =
        [
            await PostAsync(bus, options, environment, failing, null, IPAddress.Loopback),
            await PostAsync(bus, options, environment, failing, "fedcba9876543210", IPAddress.Loopback),
            await PostAsync(bus, options, environment, failing, Token, IPAddress.Parse("203.0.113.20")),
            await PostAsync(bus, new SsrOptions { Development = true }, environment, failing, Token, IPAddress.Loopback),
        ];
        var lines = await File.ReadAllLinesAsync(source, cancellationToken);
        await stop.CancelAsync();

        Assert.Equal((204, 204), (failed, cleared));
        var error = Assert.Single(errors.Errors);
        Assert.Equal(("DevelopmentBuildTests.cs", 20, 9, "CS1002", "; expected"), (error.File, error.Line, error.Column, error.Code, error.Message));
        Assert.Equal(Enumerable.Range(18, 5).Select(line => new FaultLine(line, lines[line - 1])), error.Source);
        Assert.Empty(fixedBuild.Errors);
        Assert.All(refused, status => Assert.Equal(403, status));
        Assert.StartsWith("event: build\ndata: {\"errors\":[{\"file\":\"DevelopmentBuildTests.cs\",\"line\":20,", DevelopmentChannel.Format(errors), StringComparison.Ordinal);
        Assert.Equal("event: build\ndata: {\"errors\":[]}\n\n", DevelopmentChannel.Format(fixedBuild));
    }

    private static async Task<int> PostAsync(EventBus bus, SsrOptions options, HostingEnvironment environment, string body, string? token, IPAddress from)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = from;
        http.Request.Method = "POST";
        http.Request.ContentType = "application/json";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        if (token is not null)
        {
            http.Request.Headers[DevelopmentBuild.TokenHeader] = token;
        }

        await DevelopmentBuild.ReceiveAsync(http, bus, options, environment);
        return http.Response.StatusCode;
    }

    private static string Here([CallerFilePath] string path = "") => path;
}
