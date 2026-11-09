using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.Json;
using Aioros.Functions;
using Aioros.Pages;
using Aioros.Runtime.Functions;
using Aioros.Runtime.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Aioros.Runtime.Pages;

/// <summary>
/// The installed app's loader results: one <c>GET /_data{route}?{query}</c> to the server per navigation or revalidation,
/// as Remix's single fetch and SvelteKit's <c>__data.json</c>, read back with the code the generator emitted for each
/// loader's data.
/// </summary>
public sealed class RemoteLoaderResults(HttpClient http) : ILoaderResults
{
    public const string Prefix = "/_data";

    /// <summary>Asks <c>/_data</c> for one view instance's results alone, by its key.</summary>
    public const string ViewHeader = "X-Aioros-View";

    public HttpClient Http { get; } = http;

    public async ValueTask<LoaderResults> LoadAsync(PageDefinition page, PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(request);
        using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(Address(request), UriKind.Relative));
        message.Headers.Add(ServerFunctionGroup.ContractHeader, page.DataContract);
        using var response = await Http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var outcomes = new LoaderOutcome[page.ModelCount];
        ImmutableArray<ImmutableArray<string>> keys = default;
        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
                keys = await ReadAsync(response, page, outcomes, cancellationToken);
                break;
            case HttpStatusCode.NotFound:
                outcomes[^1] = await NotFoundAsync(response, cancellationToken);
                break;
            case HttpStatusCode.UpgradeRequired:
                var server = response.Headers.TryGetValues(ServerFunctionGroup.ContractHeader, out var values) ? values.FirstOrDefault() ?? string.Empty : string.Empty;
                throw new ClientOutdatedException("The server sends this page's data in a newer shape; update the app.", server);
            case HttpStatusCode.Unauthorized:
                throw new SignIn.SignInRequiredException("The server asks for a sign-in before it sends the page's data.");
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException("The server refused the page's data (403).");
            default:
                throw new HttpRequestException($"The page's data could not be loaded ({(int)response.StatusCode}).", null, response.StatusCode);
        }

        return new LoaderResults([.. outcomes], keys);
    }

    public ValueTask<ViewResult> LoadViewAsync(PageDefinition page, PageRequest request, int view, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        return ViewAsync(page, request, view, page.Views[view].Key, cancellationToken);
    }

    /// <summary>One item of a repeated view instance, asked as <c>{key}[{item}]</c>; its result holds that item alone.</summary>
    public ValueTask<ViewResult> LoadItemAsync(PageDefinition page, PageRequest request, int view, string item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        return ViewAsync(page, request, view, ViewItems.RegionKey(page.Views[view].Key, item), cancellationToken);
    }

    private async ValueTask<ViewResult> ViewAsync(PageDefinition page, PageRequest request, int view, string named, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var instance = page.Views[view];
        using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(Address(request), UriKind.Relative));
        message.Headers.Add(ServerFunctionGroup.ContractHeader, page.DataContract);
        message.Headers.Add(ViewHeader, named);
        using var response = await Http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
                await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
                {
                    using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                    return document.RootElement.TryGetProperty("views", out var views) && views.TryGetProperty(instance.Key, out var answer) && instance.View.Loader is { } loader
                        ? new ViewResult(View(answer, loader), Keys(answer))
                        : new ViewResult(new LoaderOutcome(ViewState.Hidden), []);
                }

            case HttpStatusCode.UpgradeRequired:
                var server = response.Headers.TryGetValues(ServerFunctionGroup.ContractHeader, out var values) ? values.FirstOrDefault() ?? string.Empty : string.Empty;
                throw new ClientOutdatedException("The server sends this page's data in a newer shape; update the app.", server);
            case HttpStatusCode.Unauthorized:
                throw new SignIn.SignInRequiredException("The server asks for a sign-in before it sends the view's data.");
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException("The server refused the view's data (403).");
            default:
                throw new HttpRequestException($"The view's data could not be loaded ({(int)response.StatusCode}).", null, response.StatusCode);
        }
    }

    /// <summary>The data request of a page: <c>/_data</c>, the page's path and its query.</summary>
    public static string Address(PageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var address = new StringBuilder(Prefix).Append(request.Path.StartsWith('/') ? request.Path : "/" + request.Path);
        var separator = '?';
        foreach (var (name, value) in request.Query.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            address.Append(separator).Append(Uri.EscapeDataString(name)).Append('=').Append(Uri.EscapeDataString(value));
            separator = '&';
        }

        return address.ToString();
    }

    private static async Task<ImmutableArray<ImmutableArray<string>>> ReadAsync(HttpResponseMessage response, PageDefinition page, LoaderOutcome[] outcomes, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (root.GetProperty("type").GetString() == "redirect")
        {
            outcomes[^1] = LoaderResult.Redirect(root.GetProperty("location").GetString()!);
            return [];
        }

        var views = root.TryGetProperty("views", out var found) ? found : default;
        Read(page, root.GetProperty("segments"), views).CopyTo(outcomes);
        return ReadKeys(page, root.GetProperty("segments"), views);
    }

    /// <summary>The keys each slot's loader read on the server, by slot; empty when none read any.</summary>
    private static ImmutableArray<ImmutableArray<string>> ReadKeys(PageDefinition page, JsonElement segments, JsonElement views)
    {
        var keys = new ImmutableArray<string>[page.ModelCount];
        var any = false;
        var index = 0;
        foreach (var segment in segments.EnumerateArray())
        {
            if (index < keys.Length && segment.ValueKind == JsonValueKind.Object)
            {
                keys[index] = Keys(segment);
                any |= !keys[index].IsEmpty;
            }

            index++;
        }

        for (var view = 0; view < page.Views.Length && views.ValueKind == JsonValueKind.Object; view++)
        {
            if (views.TryGetProperty(page.Views[view].Key, out var answer))
            {
                keys[page.FirstView + view] = Keys(answer);
                any |= !keys[page.FirstView + view].IsEmpty;
            }
        }

        return any ? [.. keys.Select(static own => own.IsDefault ? [] : own)] : [];
    }

    private static async Task<LoaderOutcome> NotFoundAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        return root.TryGetProperty("segments", out var segments) && root.TryGetProperty("contract", out var contract)
            ? new LoaderOutcome(new RemoteNotFound(contract.GetString() ?? string.Empty, segments.Clone())) { NotFound = true }
            : LoaderResult.NotFound();
    }

    internal static ImmutableArray<LoaderOutcome> Read(PageDefinition page, JsonElement segments, JsonElement views = default)
    {
        var outcomes = new LoaderOutcome[page.ModelCount];
        var index = 0;
        foreach (var segment in segments.EnumerateArray())
        {
            if (index < outcomes.Length && segment.ValueKind == JsonValueKind.Object)
            {
                outcomes[index] = Segment(segment, index < page.Layouts.Length ? page.Layouts[index].Loader : page.Loader);
            }

            index++;
        }

        for (var view = 0; view < page.Views.Length && views.ValueKind == JsonValueKind.Object; view++)
        {
            var instance = page.Views[view];
            if (instance.View.Loader is { } loader && views.TryGetProperty(instance.Key, out var answer))
            {
                outcomes[page.FirstView + view] = View(answer, loader);
            }
        }

        return [.. outcomes];
    }

    private static LoaderOutcome View(JsonElement answer, LoaderDefinition loader)
    {
        if (answer.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            return new LoaderOutcome(new ItemResults([.. items.EnumerateArray().Select(item => new ItemResult(item.GetProperty("key").GetString() ?? string.Empty, null, View(item, loader)))]));
        }

        return answer.TryGetProperty("state", out var state)
            ? new LoaderOutcome(state.GetString() == ViewState.Hidden.Name ? ViewState.Hidden : ViewState.Failed)
            : new LoaderOutcome(loader.Read is { } read && answer.TryGetProperty("data", out var data) ? read(data) : null);
    }

    private static LoaderOutcome Segment(JsonElement segment, LoaderDefinition? loader) =>
        new(loader?.Read is { } read && segment.TryGetProperty("data", out var data) ? read(data) : null)
        {
            Title = segment.TryGetProperty("title", out var title) ? title.GetString() : null,
            Metadata = segment.TryGetProperty("metadata", out var metadata) ? Metadata(metadata) : null,
        };

    private static ImmutableArray<string> Keys(JsonElement answer) =>
        answer.TryGetProperty("keys", out var keys) && keys.ValueKind == JsonValueKind.Array
            ? [.. keys.EnumerateArray().Select(static key => key.GetString() ?? string.Empty)]
            : [];

    private static PageMetadata Metadata(JsonElement metadata) => new(
        Text(metadata, "description"),
        Text(metadata, "canonical"),
        Text(metadata, "image"),
        Text(metadata, "type") ?? PageMetadata.None.Type,
        !metadata.TryGetProperty("indexable", out var indexable) || indexable.GetBoolean());

    private static string? Text(JsonElement element, string name) => element.TryGetProperty(name, out var value) ? value.GetString() : null;
}

/// <summary>
/// The not-found page's loader results that the server sent with a <c>404</c>, loaded for the path that was not found, so
/// the installed app shows the not-found page from the same answer.
/// </summary>
public sealed class RemoteNotFound(string contract, JsonElement segments)
{
    public string Contract { get; } = contract;

    /// <summary>The results of the not-found page's segments, read with its loaders; fails when they come in another shape.</summary>
    public ImmutableArray<LoaderOutcome> Read(PageDefinition notFound)
    {
        ArgumentNullException.ThrowIfNull(notFound);
        return Contract == notFound.DataContract
            ? RemoteLoaderResults.Read(notFound, segments)
            : throw new ClientOutdatedException("The server sends the not-found page's data in a newer shape; update the app.", Contract);
    }
}

public static class RemoteLoaderResultsRegistration
{
    /// <summary>
    /// Registers where the installed app gets its pages' loader results: from the server at <paramref name="server"/>
    /// (<c>Aioros:Server</c>), or, without one, nowhere, so the loaders run in process as on the website.
    /// </summary>
    public static IServiceCollection AddRemoteLoaderResults(this IServiceCollection services, Uri? server)
    {
        ArgumentNullException.ThrowIfNull(services);
        return server is null ? services : services.AddSingleton<ILoaderResults>(new RemoteLoaderResults(new HttpClient { BaseAddress = server }));
    }
}
