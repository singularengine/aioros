namespace LumenBlazor.Data;

/// Same icon markup as the XamlSsr framework (Lucide-style).
public static class Icons
{
    private const string Fill = " fill=\"currentColor\" stroke=\"none\"";

    public static readonly Dictionary<string, string> Svg = new()
    {
        ["inbox"] = "<polyline points=\"22 12 16 12 14 15 10 15 8 12 2 12\"/><path d=\"M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z\"/>",
        ["issues"] = "<circle cx=\"12\" cy=\"12\" r=\"10\"/><circle cx=\"12\" cy=\"12\" r=\"1\"/>",
        ["projects"] = "<path d=\"M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13c0 1.1.9 2 2 2Z\"/><path d=\"M8 10v4\"/><path d=\"M12 10v2\"/><path d=\"M16 10v6\"/>",
        ["dashboard"] = "<rect width=\"7\" height=\"9\" x=\"3\" y=\"3\" rx=\"1\"/><rect width=\"7\" height=\"5\" x=\"14\" y=\"3\" rx=\"1\"/><rect width=\"7\" height=\"9\" x=\"14\" y=\"12\" rx=\"1\"/><rect width=\"7\" height=\"5\" x=\"3\" y=\"16\" rx=\"1\"/>",
        ["settings"] = "<line x1=\"21\" x2=\"14\" y1=\"4\" y2=\"4\"/><line x1=\"10\" x2=\"3\" y1=\"4\" y2=\"4\"/><line x1=\"21\" x2=\"12\" y1=\"12\" y2=\"12\"/><line x1=\"8\" x2=\"3\" y1=\"12\" y2=\"12\"/><line x1=\"21\" x2=\"16\" y1=\"20\" y2=\"20\"/><line x1=\"12\" x2=\"3\" y1=\"20\" y2=\"20\"/><line x1=\"14\" x2=\"14\" y1=\"2\" y2=\"6\"/><line x1=\"8\" x2=\"8\" y1=\"10\" y2=\"14\"/><line x1=\"16\" x2=\"16\" y1=\"18\" y2=\"22\"/>",
        ["search"] = "<circle cx=\"11\" cy=\"11\" r=\"8\"/><path d=\"m21 21-4.3-4.3\"/>",
        ["menu"] = "<line x1=\"4\" x2=\"20\" y1=\"12\" y2=\"12\"/><line x1=\"4\" x2=\"20\" y1=\"6\" y2=\"6\"/><line x1=\"4\" x2=\"20\" y1=\"18\" y2=\"18\"/>",
        ["plus"] = "<path d=\"M5 12h14\"/><path d=\"M12 5v14\"/>",
        ["sun"] = "<circle cx=\"12\" cy=\"12\" r=\"4\"/><path d=\"M12 2v2\"/><path d=\"M12 20v2\"/><path d=\"m4.93 4.93 1.41 1.41\"/><path d=\"m17.66 17.66 1.41 1.41\"/><path d=\"M2 12h2\"/><path d=\"M20 12h2\"/><path d=\"m6.34 17.66-1.41 1.41\"/><path d=\"m19.07 4.93-1.41 1.41\"/>",
        ["moon"] = "<path d=\"M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z\"/>",
        ["chevron-right"] = "<path d=\"m9 18 6-6-6-6\"/>",
        ["chevron-down"] = "<path d=\"m6 9 6 6 6-6\"/>",
        ["user"] = "<path d=\"M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2\"/><circle cx=\"12\" cy=\"7\" r=\"4\"/>",
        ["users"] = "<path d=\"M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2\"/><circle cx=\"9\" cy=\"7\" r=\"4\"/><path d=\"M22 21v-2a4 4 0 0 0-3-3.87\"/><path d=\"M16 3.13a4 4 0 0 1 0 7.75\"/>",
        ["log-out"] = "<path d=\"M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4\"/><polyline points=\"16 17 21 12 16 7\"/><line x1=\"21\" x2=\"9\" y1=\"12\" y2=\"12\"/>",
        ["bell"] = "<path d=\"M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9\"/><path d=\"M10.3 21a1.94 1.94 0 0 0 3.4 0\"/>",
        ["activity"] = "<path d=\"M22 12h-4l-3 9L9 3l-3 9H2\"/>",
        ["check"] = "<path d=\"M20 6 9 17l-5-5\"/>",
        ["x"] = "<path d=\"M18 6 6 18\"/><path d=\"m6 6 12 12\"/>",
        ["zap"] = "<path d=\"M4 14a1 1 0 0 1-.78-1.63l9.9-10.2a.5.5 0 0 1 .86.46l-1.92 6.02A1 1 0 0 0 13 10h7a1 1 0 0 1 .78 1.63l-9.9 10.2a.5.5 0 0 1-.86-.46l1.92-6.02A1 1 0 0 0 11 14z\"/>",
        ["status-backlog"] = "<circle cx=\"12\" cy=\"12\" r=\"9\" stroke-dasharray=\"3.5 3\"/>",
        ["status-todo"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/>",
        ["status-progress"] = $"<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 6.5a5.5 5.5 0 0 1 0 11z\"{Fill}/>",
        ["status-review"] = $"<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 6.5a5.5 5.5 0 1 1-5.5 5.5H12z\"{Fill}/>",
        ["status-done"] = $"<circle cx=\"12\" cy=\"12\" r=\"10\"{Fill}/><path d=\"m7.5 12 3 3 6-6\" stroke=\"var(--on-accent,#fff)\"/>",
        ["status-canceled"] = $"<circle cx=\"12\" cy=\"12\" r=\"10\"{Fill}/><path d=\"m9 9 6 6M15 9l-6 6\" stroke=\"var(--on-accent,#fff)\"/>",
        ["prio-urgent"] = $"<rect x=\"3\" y=\"3\" width=\"18\" height=\"18\" rx=\"4\"{Fill}/><path d=\"M12 7.5v5.5M12 16.5v.01\" stroke=\"var(--on-accent,#fff)\" stroke-width=\"2.5\"/>",
        ["prio-high"] = $"<rect x=\"3\" y=\"14\" width=\"4.5\" height=\"7\" rx=\"1\"{Fill}/><rect x=\"9.75\" y=\"9\" width=\"4.5\" height=\"12\" rx=\"1\"{Fill}/><rect x=\"16.5\" y=\"3\" width=\"4.5\" height=\"18\" rx=\"1\"{Fill}/>",
        ["prio-medium"] = $"<rect x=\"3\" y=\"14\" width=\"4.5\" height=\"7\" rx=\"1\"{Fill}/><rect x=\"9.75\" y=\"9\" width=\"4.5\" height=\"12\" rx=\"1\"{Fill}/><rect x=\"16.5\" y=\"3\" width=\"4.5\" height=\"18\" rx=\"1\" fill=\"currentColor\" stroke=\"none\" opacity=\".25\"/>",
        ["prio-low"] = $"<rect x=\"3\" y=\"14\" width=\"4.5\" height=\"7\" rx=\"1\"{Fill}/><rect x=\"9.75\" y=\"9\" width=\"4.5\" height=\"12\" rx=\"1\" fill=\"currentColor\" stroke=\"none\" opacity=\".25\"/><rect x=\"16.5\" y=\"3\" width=\"4.5\" height=\"18\" rx=\"1\" fill=\"currentColor\" stroke=\"none\" opacity=\".25\"/>",
        ["prio-none"] = "<path d=\"M4.5 12h3M10.5 12h3M16.5 12h3\"/>",
    };

    public static string Markup(string? kind) => kind != null && Svg.TryGetValue(kind, out var svg) ? svg : "";

    /// The markup of the Icon component, without a component instance: for icons repeated in every row of a list
    /// ("Avoid thousands of component instances", ASP.NET Core Blazor rendering performance best practices).
    public static Microsoft.AspNetCore.Components.MarkupString Inline(string kind, string color, int size) => new(
        $"<svg class=\"icon\" viewBox=\"0 0 24 24\" width=\"{size}\" height=\"{size}\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" style=\"color:{color}\">{Markup(kind)}</svg>");
}
