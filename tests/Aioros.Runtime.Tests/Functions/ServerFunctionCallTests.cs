using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json;
using Aioros.Functions;
using Aioros.Runtime.Functions;

namespace Aioros.Runtime.Tests.Functions;

public sealed class ServerFunctionCallTests
{
    [Fact]
    [Trait("AC", "FUNCTIONS-05")]
    public async Task FailuresBecomeDotNetExceptionsAsync()
    {
        var validation = await FailAsync<ValidationException>(HttpStatusCode.BadRequest, """
            {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"detail":"A customer needs a name.","errors":{"Name":["A customer needs a name."],"Email":["A customer needs a name."]}}
            """);
        var unprocessable = await FailAsync<ValidationException>(HttpStatusCode.UnprocessableEntity, """{"title":"Invalid.","status":422,"errors":{"email":["Not an email."]}}""");
        var titled = await FailAsync<ValidationException>(HttpStatusCode.BadRequest, """{"title":"Too many requests today.","status":400}""");
        var bare = await FailAsync<ValidationException>(HttpStatusCode.BadRequest, string.Empty);
        var missing = await FailAsync<KeyNotFoundException>(HttpStatusCode.NotFound, """{"title":"Not Found","status":404,"detail":"That customer does not exist."}""");
        var unknown = await FailAsync<KeyNotFoundException>(HttpStatusCode.NotFound, string.Empty);
        var anonymous = await FailAsync<Aioros.Runtime.SignIn.SignInRequiredException>(HttpStatusCode.Unauthorized, string.Empty);
        var forbidden = await FailAsync<UnauthorizedAccessException>(HttpStatusCode.Forbidden, """{"title":"Forbidden","status":403,"detail":"Only owners."}""");
        var failed = await FailAsync<InvalidOperationException>(HttpStatusCode.InternalServerError, """{"title":"The server function failed.","status":500}""");
        var gateway = await FailAsync<InvalidOperationException>(HttpStatusCode.BadGateway, "<html>bad gateway</html>");
        var outdated = await FailAsync<ClientOutdatedException>(HttpStatusCode.UpgradeRequired, string.Empty, ("X-Aioros-Contract", "c2"));

        Assert.Equal("A customer needs a name.", validation.ValidationResult.ErrorMessage);
        Assert.Equal(["Name", "Email"], validation.ValidationResult.MemberNames);
        Assert.Equal("A customer needs a name.", validation.Message);
        Assert.Equal("Not an email.", unprocessable.ValidationResult.ErrorMessage);
        Assert.Equal(["email"], unprocessable.ValidationResult.MemberNames);
        Assert.Equal(("Too many requests today.", 0), (titled.Message, titled.ValidationResult.MemberNames.Count()));
        Assert.False(string.IsNullOrEmpty(bare.Message));
        Assert.Equal("That customer does not exist.", missing.Message);
        Assert.False(string.IsNullOrEmpty(unknown.Message));
        Assert.False(string.IsNullOrEmpty(anonymous.Message));
        Assert.Equal("Only owners.", forbidden.Message);
        Assert.Equal("The server function failed.", failed.Message);
        Assert.Contains("502", gateway.Message, StringComparison.Ordinal);
        Assert.Equal("c2", outdated.ServerContract);
        Assert.IsNotType<HttpRequestException>(failed);
    }

    private static Task<TException> FailAsync<TException>(HttpStatusCode status, string body, params (string Name, string Value)[] headers)
        where TException : Exception =>
        Assert.ThrowsAsync<TException>(() => ServerFunctionCalls.CallAsync(
            new HttpClient(new Answer(status, body, headers)) { BaseAddress = new Uri("http://app.test") },
            "counter",
            "add",
            "c1",
            1,
            static (writer, amount) =>
            {
                writer.WriteStartObject();
                writer.WriteNumber("amount", amount);
                writer.WriteEndObject();
            },
            static element => element.GetInt32(),
            false,
            TestContext.Current.CancellationToken));

    private sealed class Answer(HttpStatusCode status, string body, (string Name, string Value)[] headers) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var sent = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.Equal(1, sent.RootElement.GetProperty("amount").GetInt32());
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/problem+json") };
            foreach (var (name, value) in headers)
            {
                response.Headers.Add(name, value);
            }

            return response;
        }
    }
}
