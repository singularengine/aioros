using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The drafts module of the framework's script, loaded only by pages whose form holds fields a visitor edits: the values
/// that differ from the ones the server last answered are kept in the browser (IndexedDB, per page address) as the
/// visitor types, and when the page is opened again with other values a notice (<c>.aioros-draft</c>) offers to restore
/// them or discard them; nothing is applied unless asked, and an answered post removes the page's draft (decided with the
/// user on 2026-10-28, the security audit's area 9).
/// </summary>
public static class DraftScript
{
    public const string Prefix = "/_aioros/draft/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("draft"));

    public static string Path => Module.Path;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
