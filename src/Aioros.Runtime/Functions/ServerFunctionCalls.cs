using System.Buffers;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Aioros.Functions;

namespace Aioros.Runtime.Functions;

/// <summary>
/// The HTTP side of a generated server function client: the arguments and the result are written and read by the code
/// the generator emitted for their types, and a failure becomes the .NET exception the in-process call would throw.
/// </summary>
public static class ServerFunctionCalls
{
    private static readonly MediaTypeHeaderValue JsonType = new("application/json") { CharSet = "utf-8" };

    public static async Task<TResult> CallAsync<TArguments, TResult>(
        HttpClient http, string group, string name, string contract, TArguments arguments, Action<Utf8JsonWriter, TArguments> write, Func<JsonElement, TResult> read, bool keyed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(read);
        using var response = await SendAsync(http, group, name, contract, arguments, write, keyed, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return read(document.RootElement);
    }

    public static async Task CallAsync<TArguments>(
        HttpClient http, string group, string name, string contract, TArguments arguments, Action<Utf8JsonWriter, TArguments> write, bool keyed, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(http, group, name, contract, arguments, write, keyed, cancellationToken);
    }

    private static async Task<HttpResponseMessage> SendAsync<TArguments>(
        HttpClient http, string group, string name, string contract, TArguments arguments, Action<Utf8JsonWriter, TArguments> write, bool keyed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(write);
        var body = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(body))
        {
            write(writer, arguments);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ServerFunctionGroup.Path(group, name), UriKind.Relative))
        {
            Content = new ReadOnlyMemoryContent(body.WrittenMemory) { Headers = { ContentType = JsonType } },
        };
        request.Headers.Add(ServerFunctionGroup.FunctionHeader, "1");
        request.Headers.Add(ServerFunctionGroup.ContractHeader, contract);
        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            Record(response, keyed);
            return response;
        }

        using (response)
        {
            throw await FailureAsync(response, cancellationToken);
        }
    }

    /// <summary>
    /// Records what a successful call changed into the running command: the keys the server answered, or, for a function
    /// that declares no keys, that the command must refresh the whole page.
    /// </summary>
    private static void Record(HttpResponseMessage response, bool keyed)
    {
        if (DataKeys.Current is not { } changes)
        {
            return;
        }

        if (!keyed)
        {
            changes.Undeclared();
            return;
        }

        if (!response.Headers.TryGetValues(ServerFunctionGroup.RevalidateHeader, out var values))
        {
            return;
        }

        foreach (var key in values.SelectMany(static value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            changes.Invalidate(Uri.UnescapeDataString(key));
        }
    }

    private static async Task<Exception> FailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        if (response.StatusCode == HttpStatusCode.UpgradeRequired)
        {
            var server = response.Headers.TryGetValues(ServerFunctionGroup.ContractHeader, out var values) ? values.FirstOrDefault() ?? string.Empty : string.Empty;
            return new ClientOutdatedException("The server runs a newer version of these functions; update the app.", server);
        }

        var problem = await ProblemAsync(response, cancellationToken);
        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new SignIn.SignInRequiredException(problem.Detail ?? "The server asks for a sign-in before this call."),
            HttpStatusCode.Forbidden => new UnauthorizedAccessException(problem.Detail ?? $"The server refused the call ({status})."),
            HttpStatusCode.NotFound => new KeyNotFoundException(problem.Detail ?? "The server found nothing for this call."),
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => new ValidationException(
                new ValidationResult(problem.Detail ?? problem.FirstError ?? problem.Title ?? "The server refused the call's data.", problem.Members), null, null),
            _ => new InvalidOperationException(problem.Title ?? $"The server function failed ({status})."),
        };
    }

    private static async Task<Problem> ProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (text.Length == 0 || text[0] != (byte)'{')
        {
            return new Problem(null, null, null, []);
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            var members = new List<string>();
            string? first = null;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var error in errors.EnumerateObject())
                {
                    members.Add(error.Name);
                    first ??= error.Value.ValueKind == JsonValueKind.Array && error.Value.GetArrayLength() > 0 ? error.Value[0].GetString() : null;
                }
            }

            return new Problem(Text(root, "title"), Text(root, "detail"), first, members);
        }
        catch (JsonException)
        {
            return new Problem(null, null, null, []);
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private sealed record Problem(string? Title, string? Detail, string? FirstError, List<string> Members);
}
