using System.Runtime.CompilerServices;
using System.Text.Json;
using Aioros.Targets.Ssr.Development;
using Aioros.Targets.Ssr.Hosting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;

namespace Aioros.Targets.Ssr.Tests.Development;

public sealed class DevelopmentFaultsTests
{
    [Fact]
    [Trait("AC", "SSR-287")]
    public async Task PageErrorsAreAnsweredWithTheirFaultAsync()
    {
        var root = Path.GetDirectoryName(Here())!;
        var faults = new DevelopmentFaults(new HostingEnvironment { ContentRootPath = root });
        var (error, line) = Thrown();
        var nexts = 0;
        Task NextAsync(ErrorContext _)
        {
            nexts++;
            return Task.CompletedTask;
        }

        var navigation = await AnswerAsync(faults, error, NextAsync, ("X-Aioros-Navigation", "1"));
        var document = await AnswerAsync(faults, error, NextAsync, ("Accept", "text/html,application/xhtml+xml"));
        var api = await AnswerAsync(faults, error, NextAsync, ("Accept", "application/json"));
        var outside = await AnswerAsync(new DevelopmentFaults(new HostingEnvironment { ContentRootPath = Path.Combine(root, "elsewhere") }), error, NextAsync, ("X-Aioros-Navigation", "1"));
        var named = await AnswerAsync(new DevelopmentFaults(new HostingEnvironment { ContentRootPath = Path.Combine(root, "elsewhere"), ApplicationName = "Aioros.Targets.Ssr.Tests" }), error, NextAsync, ("X-Aioros-Navigation", "1"));
        using var fault = JsonDocument.Parse(navigation.Body);
        var code = fault.RootElement.GetProperty("code").EnumerateArray().Select(entry => entry.GetProperty("line").GetInt32()).ToList();
        var development = new ServiceCollection().AddAiorosPages(new SsrOptions { Development = true });
        var production = new ServiceCollection().AddAiorosPages(new SsrOptions());

        Assert.Equal((500, "1"), (navigation.Http.Response.StatusCode, navigation.Http.Response.Headers["X-Aioros-Fault"].ToString()));
        Assert.Equal("System.InvalidOperationException", fault.RootElement.GetProperty("type").GetString());
        Assert.Equal("Message broke", fault.RootElement.GetProperty("message").GetString());
        Assert.Equal("DevelopmentFaultsTests.cs", fault.RootElement.GetProperty("file").GetString());
        Assert.Equal(line, fault.RootElement.GetProperty("line").GetInt32());
        Assert.Equal([line - 2, line - 1, line, line + 1, line + 2], code);
        Assert.Contains("Thrown", fault.RootElement.GetProperty("stack").GetString(), StringComparison.Ordinal);
        Assert.Equal(500, document.Http.Response.StatusCode);
        Assert.StartsWith("text/html", document.Http.Response.ContentType, StringComparison.Ordinal);
        Assert.Contains("Message broke", document.Body, StringComparison.Ordinal);
        Assert.Contains("/_aioros/dev", document.Body, StringComparison.Ordinal);
        Assert.Equal(1, nexts);
        Assert.Empty(api.Body);
        Assert.Equal(JsonValueKind.Null, JsonDocument.Parse(outside.Body).RootElement.GetProperty("file").ValueKind);
        Assert.Equal(JsonValueKind.Null, JsonDocument.Parse(outside.Body).RootElement.GetProperty("at").ValueKind);
        Assert.Equal("Aioros.Targets.Ssr.Tests.Development.DevelopmentFaultsTests.Thrown()", JsonDocument.Parse(named.Body).RootElement.GetProperty("at").GetString());
        Assert.Contains(development, service => service.ServiceType == typeof(IDeveloperPageExceptionFilter));
        Assert.DoesNotContain(production, service => service.ServiceType == typeof(IDeveloperPageExceptionFilter));
    }

    private static async Task<(DefaultHttpContext Http, string Body)> AnswerAsync(DevelopmentFaults faults, Exception error, Func<ErrorContext, Task> next, (string Name, string Value) header)
    {
        var http = new DefaultHttpContext();
        http.Request.Headers[header.Name] = header.Value;
        using var body = new MemoryStream();
        http.Response.Body = body;
        await faults.HandleExceptionAsync(new ErrorContext(http, new InvalidOperationException("Wrapped", error)), next);
        return (http, System.Text.Encoding.UTF8.GetString(body.ToArray()));
    }

    private static (Exception Error, int Line) Thrown()
    {
        try
        {
            throw new InvalidOperationException("Message broke");
        }
        catch (InvalidOperationException error)
        {
            return (error, Line() - 4);
        }
    }

    private static int Line([CallerLineNumber] int line = 0) => line;

    private static string Here([CallerFilePath] string path = "") => path;
}
