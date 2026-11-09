using System;
using System.Collections.Generic;
using System.Linq;

namespace Aioros.Routing.Files;

public static class FileRoutes
{
    public const string PagesFolder = "Pages";
    public const string PageFile = "Page.axaml";
    public const string LayoutFile = "Layout.axaml";
    public const string NotFoundFile = "NotFound.axaml";
    public const string NotFoundRoute = "/_not-found";
    public const string ErrorFile = "Error.axaml";
    public const string ErrorRoute = "/_error";
    public const string NativeOnlyFile = "NativeOnly.axaml";
    public const string NativeOnlyRoute = "/_native-only";

    private static readonly char[] Separators = ['/', '\\'];

    public static string? RouteOf(string file)
    {
        var parts = Parts(file);
        if (parts.Length < 2 || parts[0] != PagesFolder || parts[parts.Length - 1] != PageFile)
        {
            return null;
        }

        var segments = new List<string>();
        for (var index = 1; index < parts.Length - 1; index++)
        {
            var folder = parts[index];
            if (Starts(folder, '_'))
            {
                return null;
            }

            if (Segment(folder) is { } segment)
            {
                segments.Add(segment);
            }
        }

        return "/" + string.Join("/", segments);
    }

    public static FileRouteMap Map(IEnumerable<string> files)
    {
        var normalized = files.Select(Normalize).Distinct(StringComparer.Ordinal).OrderBy(file => file, StringComparer.Ordinal).ToList();
        var layouts = new HashSet<string>(normalized.Where(IsLayout), StringComparer.Ordinal);
        var routes = new List<FileRoute>();
        var conflicts = new List<FileRouteConflict>();
        var seen = new Dictionary<string, FileRoute>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in normalized)
        {
            if (RouteOf(file) is not { } route)
            {
                continue;
            }

            var page = new FileRoute(route, file, LayoutsOf(file, layouts));
            if (seen.TryGetValue(Shape(route), out var existing))
            {
                conflicts.Add(new FileRouteConflict(route, existing.Source, file));
                continue;
            }

            seen[Shape(route)] = page;
            routes.Add(page);
        }

        var notFound = PagesFolder + "/" + NotFoundFile;
        var notFoundPage = normalized.Contains(notFound) ? new FileRoute(NotFoundRoute, notFound, LayoutsOf(notFound, layouts)) : null;
        var error = PagesFolder + "/" + ErrorFile;
        var errorPage = normalized.Contains(error) ? new FileRoute(ErrorRoute, error, LayoutsOf(error, layouts)) : null;
        var nativeOnly = PagesFolder + "/" + NativeOnlyFile;
        var nativeOnlyPage = normalized.Contains(nativeOnly) ? new FileRoute(NativeOnlyRoute, nativeOnly, LayoutsOf(nativeOnly, layouts)) : null;
        return new FileRouteMap(routes, conflicts, notFoundPage, errorPage, nativeOnlyPage);
    }

    public static string Normalize(string file) => string.Join("/", Parts(file));

    private static bool IsLayout(string file)
    {
        var parts = Parts(file);
        return parts.Length >= 2 && parts[0] == PagesFolder && parts[parts.Length - 1] == LayoutFile
            && !parts.Any(part => Starts(part, '_'));
    }

    private static List<string> LayoutsOf(string page, HashSet<string> layouts)
    {
        var parts = Parts(page);
        var chain = new List<string>();
        for (var depth = 1; depth < parts.Length; depth++)
        {
            var candidate = string.Join("/", parts.Take(depth)) + "/" + LayoutFile;
            if (layouts.Contains(candidate))
            {
                chain.Add(candidate);
            }
        }

        return chain;
    }

    private static string? Segment(string folder)
    {
        if (Starts(folder, '(') && Ends(folder, ')'))
        {
            return null;
        }

        if (!Starts(folder, '[') || !Ends(folder, ']'))
        {
            return folder;
        }

        var name = folder.Trim('[', ']');
        return name.StartsWith("...", StringComparison.Ordinal) ? "{*" + name.Remove(0, 3) + "}" : "{" + name + "}";
    }

    private static string Shape(string route) =>
        string.Join("/", route.Split('/').Select(segment => Starts(segment, '{') ? ShapeOf(segment) : segment));

    private static string ShapeOf(string parameter)
    {
        if (parameter.StartsWith("{*", StringComparison.Ordinal))
        {
            return "{*}";
        }

        var colon = parameter.IndexOf(':');
        return colon < 0 ? "{}" : "{:" + parameter.Remove(0, colon + 1);
    }

    private static bool Starts(string text, char first) => text.Length > 0 && text[0] == first;

    private static bool Ends(string text, char last) => text.Length > 0 && text[text.Length - 1] == last;

    private static string[] Parts(string file) => file.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
}
