using Microsoft.CodeAnalysis;

namespace Aioros.Generators.Pages;

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor UnknownModel = new(
        "AIOROS001",
        "Unknown view model",
        "{0}: {1}",
        "Aioros",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RouteConflict = new(
        "AIOROS002",
        "Two pages resolve to the same route",
        "'{1}' and '{2}' both resolve to the route '{0}'",
        "Aioros",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DataWithoutLoader = new(
        "AIOROS004",
        "The view model takes data no loader provides",
        "{0}: the view model '{1}' takes '{2}', which no loader of this page or layout returns; add a loader beside it that returns it",
        "Aioros",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LoaderDataNotTaken = new(
        "AIOROS005",
        "The view model does not take its loader's data",
        "{0}: the loader '{1}' returns '{2}', which {3}",
        "Aioros",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MisplacedLoader = new(
        "AIOROS006",
        "A loader is not beside its page or layout",
        "'{0}' is a loader, but {1}",
        "Aioros",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DeferredData = new(
        "AIOROS007",
        "Loader data is not materialized",
        "The loader '{0}' returns '{1}'{2}, which is evaluated lazily; the data must be materialized (a list or array) because the loader's scope is disposed when it returns, and because it is sent to the installed app",
        "Aioros",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AmbiguousReceive = new(
        "AIOROS008",
        "The view model cannot take new data unambiguously",
        "{0}: the view model '{1}' cannot take new '{2}' through Receive: {3}",
        "Aioros",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LostAfterCommand = new(
        "AIOROS017",
        "A view model with commands cannot take new loader data",
        "{0}: the view model '{1}' takes '{2}' from its loader and has commands but no 'Receive({2})'; after a command the loaders run again and the view model is created again from the fresh data, so it loses what the command set; add a 'public void Receive({2})' to keep it and take the new data",
        "Aioros",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnresolvedView = new(
        "AIOROS018",
        "A view cannot be placed",
        "{0}: {1}",
        "Aioros",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PerItemLoader = new(
        "AIOROS020",
        "A view in a list loads each item on its own",
        "{0}: {1}",
        "Aioros",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor Unrepeatable = new(
        "AIOROS021",
        "A view cannot repeat in its list",
        "{0}: {1}",
        "Aioros",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ChosenAtRunTime = new(
        "AIOROS022",
        "A view is chosen at run time",
        "{0}",
        "Aioros",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CachedUserData = new(
        "AIOROS024",
        "Cached data could depend on the user",
        "{0}",
        "Aioros",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnboundLoader = new(
        "AIOROS025",
        "A loader's method cannot be called from the request",
        "The loader '{0}' cannot be called: {1}",
        "Aioros",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ServerPlatform = new(
        "AIOROS030",
        "Avalonia's OnPlatform renders the server's value on the web",
        "{0}: Avalonia's OnPlatform is evaluated on the operating system that loads the XAML, so the web would render the server's value; use a:OnPlatform, which has the same options and a Web value",
        "Aioros",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnknownPlatform = new(
        "AIOROS031",
        "Unknown platform",
        "{0}: a:Page.Platforms names '{1}', which is not a platform; the platforms are Web, Desktop, Mobile",
        "Aioros",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnreadableData = new(
        "AIOROS011",
        "Loader data cannot be sent to the installed app",
        "The loader '{0}' returns '{1}', which cannot be sent to the installed app: {2}",
        "Aioros",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
