using System.Text;
using System.Text.Json;
using Aioros.Runtime.Functions;
using Aioros.Targets.Ssr.Functions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public sealed class KeyedFunctionTests
{
    private const string Contract = "k1";

    private static readonly ServerFunctionGroup Group = new(
        typeof(object),
        "issues",
        Contract,
        [
            new ServerFunctionRoute("save", static (_, _, _) => ChangedAsync(null, "issues", "issue:LUM 7,x")) { Keyed = true },
            new ServerFunctionRoute("read", static (_, _, _) => ChangedAsync(null)) { Keyed = true },
            new ServerFunctionRoute("fail", static (_, _, _) => ChangedAsync(new InvalidOperationException("The store is down."), "issues")) { Keyed = true },
            new ServerFunctionRoute("plain", static (_, _, _) => ChangedAsync(null, "issues")),
        ],
        static http => http);

    [Fact]
    [Trait("AC", "FUNCTIONS-17")]
    public async Task CallsAnswerAndRecordTheKeysTheyInvalidatedAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging().AddSingleton(new object());
        await using var app = builder.Build();
        app.MapServerFunctions([Group]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        var http = app.GetTestClient();

        var saved = await PostAsync(http, "save");
        var read = await PostAsync(http, "read");
        var failed = await PostAsync(http, "fail");
        var plain = await PostAsync(http, "plain");
        var declared = DataKeys.Begin();
        await CallAsync(http, "save", keyed: true);
        await CallAsync(http, "read", keyed: true);
        var recorded = (string.Join("|", declared.Invalidated.Order(StringComparer.Ordinal)), declared.Unknown);
        var undeclared = DataKeys.Begin();
        await CallAsync(http, "plain", keyed: false);
        DataKeys.End();
        var outside = await Record.ExceptionAsync(() => CallAsync(http, "save", keyed: true));

        Assert.Equal((204, "issue%3ALUM%207%2Cx,issues"), saved);
        Assert.Equal((204, null), read);
        Assert.Equal((500, null), failed);
        Assert.Equal((204, null), plain);
        Assert.Equal(("issue:LUM 7,x|issues", false), recorded);
        Assert.Equal((true, true), (undeclared.Unknown, undeclared.Invalidated.IsEmpty));
        Assert.Null(outside);
    }

    private static Task<object?> ChangedAsync(Exception? failure, params string[] keys)
    {
        foreach (var key in keys)
        {
            DataKeys.Current?.Invalidate(key);
        }

        return failure is null ? Task.FromResult<object?>(null) : Task.FromException<object?>(failure);
    }

    private static async Task<(int Status, string? Keys)> PostAsync(HttpClient http, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ServerFunctionGroup.Path("issues", name)) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        request.Headers.Add(ServerFunctionGroup.FunctionHeader, "1");
        request.Headers.Add(ServerFunctionGroup.ContractHeader, Contract);
        using var response = await http.SendAsync(request, TestContext.Current.CancellationToken);
        return ((int)response.StatusCode, response.Headers.TryGetValues(ServerFunctionGroup.RevalidateHeader, out var keys) ? string.Join(";", keys) : null);
    }

    private static Task CallAsync(HttpClient http, string name, bool keyed) =>
        ServerFunctionCalls.CallAsync(http, "issues", name, Contract, 0, static (writer, _) => Empty(writer), keyed, TestContext.Current.CancellationToken);

    private static void Empty(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteEndObject();
    }
}
