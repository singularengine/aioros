using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Aioros.Runtime.Functions;
using Aioros.Targets.Ssr.CrossSite;
using Aioros.Targets.Ssr.Functions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Targets.Ssr.Tests.Pages;

public interface ICounter
{
    Task<int> AddAsync(int amount, CancellationToken cancellationToken);

    Task ResetAsync(CancellationToken cancellationToken);

    Task<int> FailAsync(string kind, CancellationToken cancellationToken);

    Task<string?> NothingAsync(CancellationToken cancellationToken);
}

public sealed class Counter : ICounter
{
    public int Value { get; private set; } = 40;

    public Task<int> AddAsync(int amount, CancellationToken cancellationToken)
    {
        Value += amount;
        return Task.FromResult(Value);
    }

    public Task ResetAsync(CancellationToken cancellationToken)
    {
        Value = 0;
        return Task.CompletedTask;
    }

    public Task<int> FailAsync(string kind, CancellationToken cancellationToken) => kind switch
    {
        "validation" => throw new ValidationException(new ValidationResult("A name and an email are required.", ["Name", "Email"]), null, null),
        "message" => throw new ValidationException("Too many requests today."),
        "missing" => throw new KeyNotFoundException("That customer does not exist."),
        "refused" => throw new UnauthorizedAccessException("Only owners may do this."),
        "late" => LateAsync(),
        _ => throw new ArgumentException("secret detail"),
    };

    public Task<string?> NothingAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);

    private static async Task<int> LateAsync()
    {
        await Task.Yield();
        throw new InvalidOperationException("secret detail");
    }
}

public sealed class RemoteCounter(HttpClient http) : ICounter
{
    public HttpClient Http { get; } = http;

    public Task<int> AddAsync(int amount, CancellationToken cancellationToken) =>
        ServerFunctionCalls.CallAsync(Http, "counter", "add", "c1", amount, static (w, a) => Object(w, "amount", a), static e => e.GetInt32(), false, cancellationToken);

    public Task ResetAsync(CancellationToken cancellationToken) =>
        ServerFunctionCalls.CallAsync(Http, "counter", "reset", "c1", 0, static (w, _) => Object(w, null, 0), false, cancellationToken);

    public Task<int> FailAsync(string kind, CancellationToken cancellationToken) =>
        ServerFunctionCalls.CallAsync(Http, "counter", "fail", "c1", kind, static (w, a) =>
        {
            w.WriteStartObject();
            w.WriteString("kind", a);
            w.WriteEndObject();
        }, static e => e.GetInt32(), false, cancellationToken);

    public Task<string?> NothingAsync(CancellationToken cancellationToken) =>
        ServerFunctionCalls.CallAsync(Http, "counter", "nothing", "c1", 0, static (w, _) => Object(w, null, 0), static e => e.GetString(), false, cancellationToken);

    private static void Object(Utf8JsonWriter writer, string? name, int value)
    {
        writer.WriteStartObject();
        if (name is not null)
        {
            writer.WriteNumber(name, value);
        }

        writer.WriteEndObject();
    }
}

public sealed class LocalCounter(ICounter counter) : ICounter
{
    public async Task<int> AddAsync(int amount, CancellationToken cancellationToken)
    {
        try
        {
            return await counter.AddAsync(amount, cancellationToken);
        }
        catch (Exception error) when (!ServerFunctionFailures.Passes(error))
        {
            throw ServerFunctionFailures.Failed(error);
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        try
        {
            await counter.ResetAsync(cancellationToken);
        }
        catch (Exception error) when (!ServerFunctionFailures.Passes(error))
        {
            throw ServerFunctionFailures.Failed(error);
        }
    }

    public async Task<int> FailAsync(string kind, CancellationToken cancellationToken)
    {
        try
        {
            return await counter.FailAsync(kind, cancellationToken);
        }
        catch (Exception error) when (!ServerFunctionFailures.Passes(error))
        {
            throw ServerFunctionFailures.Failed(error);
        }
    }

    public async Task<string?> NothingAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await counter.NothingAsync(cancellationToken);
        }
        catch (Exception error) when (!ServerFunctionFailures.Passes(error))
        {
            throw ServerFunctionFailures.Failed(error);
        }
    }
}

public sealed class FunctionTests
{
    internal static readonly ServerFunctionGroup Group = new(
        typeof(ICounter),
        "counter",
        "c1",
        [
            new ServerFunctionRoute("add", async (service, arguments, ct) => await ((ICounter)service).AddAsync(arguments.GetProperty("amount").GetInt32(), ct)) { Write = Number },
            new ServerFunctionRoute("reset", async (service, _, ct) =>
            {
                await ((ICounter)service).ResetAsync(ct);
                return null;
            }),
            new ServerFunctionRoute("fail", async (service, arguments, ct) => await ((ICounter)service).FailAsync(arguments.GetProperty("kind").GetString()!, ct)) { Write = Number },
            new ServerFunctionRoute("nothing", async (service, _, ct) => await ((ICounter)service).NothingAsync(ct)) { Write = static (w, value) => w.WriteStringValue((string?)value) },
            new ServerFunctionRoute("admin", (_, _, _) => Task.FromResult<object?>("ran")) { Access = [new ServerFunctionAccess(null, null), new ServerFunctionAccess("admins", null)], Write = Text },
            new ServerFunctionRoute("member", (_, _, _) => Task.FromResult<object?>("ran")) { Access = [new ServerFunctionAccess(null, null)], Write = Text },
            new ServerFunctionRoute("owner", (_, _, _) => Task.FromResult<object?>("ran")) { Access = [new ServerFunctionAccess(null, "owner,admin")], Write = Text },
        ],
        http => new RemoteCounter(http))
    {
        InProcess = static counter => new LocalCounter((ICounter)counter),
    };

    [Fact]
    [Trait("AC", "FUNCTIONS-03")]
    public async Task CallsAreDispatchedToTheServerImplementationAsync()
    {
        var counter = new Counter();
        var added = await CallAsync(counter, "add", "{\"amount\":2}");
        var reset = await CallAsync(counter, "reset", "{}");
        var nothing = await CallAsync(counter, "nothing", "{}");
        var unmarked = await CallAsync(counter, "add", "{\"amount\":1}", header: false);
        var outdated = await CallAsync(counter, "add", "{\"amount\":1}", contract: "old");
        var unknown = await CallAsync(counter, "nope", "{}");

        Assert.Equal((200, "42", "application/json; charset=utf-8"), (added.Response.StatusCode, Body(added), added.Response.ContentType));
        Assert.Equal((204, 0), (reset.Response.StatusCode, counter.Value));
        Assert.Equal((200, "null"), (nothing.Response.StatusCode, Body(nothing)));
        Assert.Equal(403, unmarked.Response.StatusCode);
        Assert.Equal((426, "c1"), (outdated.Response.StatusCode, outdated.Response.Headers[ServerFunctionGroup.ContractHeader].ToString()));
        Assert.Equal(404, unknown.Response.StatusCode);
    }

    [Fact]
    [Trait("AC", "FUNCTIONS-18")]
    public async Task CrossSiteCallsAreRefusedBeforeTheImplementationRunsAsync()
    {
        var counter = new Counter();
        var crossSite = await CallAsync(counter, "add", "{\"amount\":1}", site: "cross-site", origin: "https://evil.test");
        var oldBrowser = await CallAsync(counter, "add", "{\"amount\":1}", origin: "https://evil.test");
        var bypassed = await CallAsync(counter, "reset", "{}", site: "cross-site", origin: "https://evil.test", protection: new CrossOriginProtection { InsecureBypassPatterns = ["/_sf/counter/reset"] });
        var sameOrigin = await CallAsync(counter, "add", "{\"amount\":2}", site: "same-origin", origin: "https://app.test");
        var installed = await CallAsync(counter, "add", "{\"amount\":3}");

        Assert.Equal(403, crossSite.Response.StatusCode);
        Assert.Equal(403, oldBrowser.Response.StatusCode);
        Assert.Equal(204, bypassed.Response.StatusCode);
        Assert.Equal((200, "2"), (sameOrigin.Response.StatusCode, Body(sameOrigin)));
        Assert.Equal((200, "5"), (installed.Response.StatusCode, Body(installed)));
        Assert.Equal(5, counter.Value);
    }

    [Fact]
    [Trait("AC", "FUNCTIONS-11")]
    public async Task ExceptionsBecomeProblemDetailsAsync()
    {
        var validation = await CallAsync(new Counter(), "fail", """{"kind":"validation"}""");
        var message = await CallAsync(new Counter(), "fail", """{"kind":"message"}""");
        var missing = await CallAsync(new Counter(), "fail", """{"kind":"missing"}""");
        var anonymous = await CallAsync(new Counter(), "fail", """{"kind":"refused"}""");
        var signedIn = await CallAsync(new Counter(), "fail", """{"kind":"refused"}""", user: User("member"));
        var unexpected = await CallAsync(new Counter(), "fail", """{"kind":"other"}""");
        var late = await CallAsync(new Counter(), "fail", """{"kind":"late"}""");

        Assert.Equal((400, "application/problem+json"), (validation.Response.StatusCode, validation.Response.ContentType));
        Assert.Equal(
            """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"detail":"A name and an email are required.","errors":{"Name":["A name and an email are required."],"Email":["A name and an email are required."]}}""",
            Body(validation));
        Assert.Equal(
            """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"detail":"Too many requests today."}""",
            Body(message));
        Assert.Equal((404, "That customer does not exist."), (missing.Response.StatusCode, Detail(missing)));
        Assert.Equal((401, "Only owners may do this."), (anonymous.Response.StatusCode, Detail(anonymous)));
        Assert.Equal((403, "Only owners may do this."), (signedIn.Response.StatusCode, Detail(signedIn)));
        Assert.Equal((500, "application/problem+json"), (unexpected.Response.StatusCode, unexpected.Response.ContentType));
        Assert.Equal("""{"title":"The server function failed.","status":500}""", Body(unexpected));
        Assert.Equal(Body(unexpected), Body(late));
        Assert.DoesNotContain("Argument", Body(unexpected), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("AC", "FUNCTIONS-04")]
    public async Task AuthorizedFunctionsCheckTheUserAsync()
    {
        var anonymous = await CallAsync(new Counter(), "admin", "{}");
        var member = await CallAsync(new Counter(), "admin", "{}", user: User("member"));
        var admin = await CallAsync(new Counter(), "admin", "{}", user: User("admin"));
        var signedIn = await CallAsync(new Counter(), "member", "{}", user: User("member"));
        var memberAsOwner = await CallAsync(new Counter(), "owner", "{}", user: User("member"));
        var owner = await CallAsync(new Counter(), "owner", "{}", user: User("owner"));
        var open = await CallAsync(new Counter(), "add", "{\"amount\":1}");

        Assert.Equal(401, anonymous.Response.StatusCode);
        Assert.Equal(403, member.Response.StatusCode);
        Assert.Equal((200, "\"ran\""), (admin.Response.StatusCode, Body(admin)));
        Assert.Equal(200, signedIn.Response.StatusCode);
        Assert.Equal((403, 200), (memberAsOwner.Response.StatusCode, owner.Response.StatusCode));
        Assert.Equal(200, open.Response.StatusCode);
    }

    [Fact]
    [Trait("AC", "FUNCTIONS-06")]
    public void InterfacesResolveToTheImplementationOrTheClient()
    {
        var local = new ServiceCollection().AddServerFunction<ICounter, Counter>([Group], server: null).BuildServiceProvider();
        var bare = new ServiceCollection().AddServerFunction<ICounter, Counter>([Group with { InProcess = null }], server: null).BuildServiceProvider();
        var remote = new ServiceCollection().AddServerFunction<ICounter, Counter>([Group], new Uri("https://api.example.test")).BuildServiceProvider();

        using var scope = local.CreateScope();
        using var bareScope = bare.CreateScope();
        Assert.IsType<LocalCounter>(scope.ServiceProvider.GetRequiredService<ICounter>());
        Assert.IsType<Counter>(bareScope.ServiceProvider.GetRequiredService<ICounter>());
        var client = Assert.IsType<RemoteCounter>(remote.GetRequiredService<ICounter>());
        Assert.Equal(new Uri("https://api.example.test"), client.Http.BaseAddress);
    }

    [Fact]
    [Trait("AC", "FUNCTIONS-13")]
    public async Task InProcessAndRemoteCallsFailAlikeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging().AddAuthorization().AddServerFunction<ICounter, Counter>([Group], server: null);
        await using var app = builder.Build();
        app.MapServerFunctions([Group]);
        await app.StartAsync(TestContext.Current.CancellationToken);
        var remote = (ICounter)Group.Client(app.GetTestClient());
        using var scope = app.Services.CreateScope();
        var local = scope.ServiceProvider.GetRequiredService<ICounter>();

        foreach (var kind in new[] { "validation", "message", "missing", "refused", "other", "late" })
        {
            var inProcess = await Record.ExceptionAsync(() => local.FailAsync(kind, TestContext.Current.CancellationToken));
            var overHttp = await Record.ExceptionAsync(() => remote.FailAsync(kind, TestContext.Current.CancellationToken));

            Assert.NotNull(inProcess);
            Assert.IsAssignableFrom(inProcess.GetType(), overHttp);
            Assert.Equal(inProcess.Message, overHttp.Message);
            if (inProcess is ValidationException expected)
            {
                Assert.Equal(expected.ValidationResult.MemberNames, ((ValidationException)overHttp).ValidationResult.MemberNames);
            }
        }

        Assert.Equal((42, 42), (await local.AddAsync(2, TestContext.Current.CancellationToken), await remote.AddAsync(2, TestContext.Current.CancellationToken)));
        Assert.Null(await remote.NothingAsync(TestContext.Current.CancellationToken));
        Assert.IsType<InvalidOperationException>(await Record.ExceptionAsync(() => local.FailAsync("other", TestContext.Current.CancellationToken)));
    }

    private static void Number(Utf8JsonWriter writer, object? value) => writer.WriteNumberValue((int)value!);

    private static void Text(Utf8JsonWriter writer, object? value) => writer.WriteStringValue((string?)value);

    private static ClaimsPrincipal User(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ada"), new Claim(ClaimTypes.Role, role)], "test"));

    private static async Task<DefaultHttpContext> CallAsync(Counter counter, string name, string json, bool header = true, string contract = "c1", ClaimsPrincipal? user = null, string? site = null, string? origin = null, CrossOriginProtection? protection = null)
    {
        var services = new ServiceCollection().AddLogging().AddSingleton<ICounter>(counter);
        services.AddAuthorizationCore(options => options.AddPolicy("admins", policy => policy.RequireRole("admin")));
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        http.Request.Method = "POST";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        http.Request.ContentLength = json.Length;
        http.Response.Body = new MemoryStream();
        if (header)
        {
            http.Request.Headers[ServerFunctionGroup.FunctionHeader] = "1";
        }

        http.Request.Headers[ServerFunctionGroup.ContractHeader] = contract;
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("app.test");
        if (site is not null)
        {
            http.Request.Headers[CrossOriginProtection.FetchSiteHeader] = site;
        }

        if (origin is not null)
        {
            http.Request.Headers.Origin = origin;
        }

        http.User = user ?? new ClaimsPrincipal(new ClaimsIdentity());
        await ServerFunctionEndpoints.DispatchAsync(http, [Group], "counter", name, protection);
        return http;
    }

    private static string Body(HttpContext http) => Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray());

    private static string? Detail(HttpContext http)
    {
        using var problem = JsonDocument.Parse(Body(http));
        return problem.RootElement.GetProperty("detail").GetString();
    }
}
