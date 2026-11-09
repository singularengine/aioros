using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The answer module of the framework's script, loaded only by pages holding a button that waits for an answer
/// (<c>data-aioros-needs-answer</c>, Aioros.Cosmo's questionnaire's Next): the button is enabled, with no request, while
/// the region around it that holds the answers (<c>data-aioros-answers</c>) shows a checked choice or a filled text input,
/// and disabled again when it shows none, as shadcn/ui's example disables Next until its item is answered.
/// </summary>
public static class AnswerScript
{
    public const string Prefix = "/_aioros/answer/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("answer"));

    public static string Path => Module.Path;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
