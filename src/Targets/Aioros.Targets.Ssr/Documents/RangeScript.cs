using Microsoft.AspNetCore.Http;

namespace Aioros.Targets.Ssr.Documents;

/// <summary>
/// The range module of the framework's script, loaded only by pages that hold a range (<c>data-aioros-range</c>, two
/// range inputs that are one two-thumb slider): as Radix Slider does, a press on the range moves the closest thumb to the
/// pointer and drags it (the inputs' own drag prevented), the values stay ordered (a thumb moved past the other becomes
/// the upper and keeps the focus), the arrows move a thumb by its step, Shift with an arrow and the page keys by ten
/// steps, Home moves the lower thumb to the minimum and End the upper to the maximum, every value snapped to the step;
/// each input whose value changes fires <c>input</c> and <c>change</c>, so its fill and the form follow.
/// </summary>
public static class RangeScript
{
    public const string Prefix = "/_aioros/range/";

    private static readonly ScriptModule Module = new(Prefix, ScriptSources.Read("range"));

    /// <summary>The module's path, named by its hash.</summary>
    public static string Path => Module.Path;

    public static Task ServeAsync(HttpContext http, string name) => Module.ServeAsync(http, name);
}
