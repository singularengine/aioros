using System.Collections.Immutable;

namespace Showcase.Ports;

public sealed record PortChange(string Part, string Lines, string What);

public sealed record Port(
    string Slug,
    string Name,
    string Kind,
    string Summary,
    string Source,
    string SourceUrl,
    string OriginalCapture,
    string Unchanged,
    string Web,
    string Native,
    string Precompiled,
    string AiorosCapture,
    string Sample,
    ImmutableArray<PortChange> Changes);

/// <summary>The open-source Avalonia apps ported to Aioros (each sample's NOTICE.md).</summary>
public static class PortCatalog
{
    public const string Captures = "ports";

    public static string Image(string slug, string kind) => $"/{Captures}/{slug}.{kind}.png";

    public static ImmutableArray<Port> All { get; } =
    [
        new(
            "kartova",
            "Kartova",
            "Disk-usage analyzer",
            "Scans a drive and draws where the space went: a drive picker with capacity rings, a treemap and a sunburst it draws itself, file types and cleanup. It reads the visitor's disks, deletes to the trash and opens the file manager, so on Aioros it stays a native app; the web serves its about page and a page that offers the app.",
            "markusfurtlehner/kartova at db28c76 (MIT): Avalonia 11.3.18 on .NET 9, CommunityToolkit, 7 views, 6,850 lines of C# and a 5,069-line core library",
            "https://github.com/markusfurtlehner/kartova",
            "Upstream at db28c76 on Avalonia 11.3.18, drawn by Avalonia headless (Skia) at 1280 by 800 by bench/ports/captures.sh: the drive picker, in its default dark theme.",
            "The 9 view model files, the 6 other views, the 3 self-drawn controls, the converters, the localization, the services and the whole core library.",
            "/about, and at / the page that offers the native app (Open in the app, Download).",
            "The whole scanner: Kartova's six screens are states of one view model, so they stay one page.",
            "1 of 1 web page (/about).",
            "The web version's start page in Chromium at 1280 by 800: Kartova is native only, so / offers the app.",
            "samples/Kartova",
            [
                new("Upgrade to Avalonia 12", "7 files, +14 / −20", "Package versions and net10.0, Watermark to PlaceholderText, the window decorations, the clipboard and Bitmap.Save."),
                new("Composition", "6 files, +44 / −150", "Program.cs becomes the Aioros builder with two registrations; App keeps the language and theme but no longer creates the window."),
                new("The window", "2 files, +31 / −182", "MainWindow becomes a user control placed by the page; the custom caption buttons and screen placement go, the window is the framework's."),
                new("Pages", "2 files, +59 / −0", "The page that places the main view (native only) and the About dialog as a web page."),
            ]),
        new(
            "advancedtodolist",
            "AdvancedToDoList",
            "To-do app on SQLite",
            "Avalonia's complete to-do sample: items with categories, priorities, due dates and progress, search, filters and sorting, a hamburger menu, overlay dialogs, import and export. On the web each visitor gets a private in-memory database seeded with the sample's data; the desktop keeps the local file.",
            "AvaloniaUI/Avalonia.Samples, CompleteApps/AdvancedToDoList at daa4000 (MIT): Avalonia 12.1.3, CommunityToolkit, SQLite through Dapper.AOT, DynamicData; 3,718 significant lines",
            "https://github.com/AvaloniaUI/Avalonia.Samples/tree/main/src/Avalonia.Samples/CompleteApps/AdvancedToDoList",
            "Upstream at daa4000 on Avalonia 12.1.3 (its desktop head), drawn by Avalonia headless (Skia) at 1280 by 800 by bench/ports/captures.sh, its database seeded with the sample's own data as the web demo is.",
            "The view models stay plain CommunityToolkit view models with no Aioros type (+160 / −232 of 860 lines, 41 of them a new confirmation view model); SharedControls changed by 4 lines.",
            "All 8 pages (6 routes): create, edit, delete, filter, sort, complete, clear and export.",
            "All 8 pages, plus import, export and appearance, on the local file or against a remote server.",
            "6 of 6 routes.",
            "The web version's item list in Chromium at 1280 by 800.",
            "samples/AdvancedToDoList",
            [
                new("Navigation", "+90 / −140", "The HamburgerMenu became a layout of links around an outlet; the views became pages."),
                new("Template selectors", "+38 / −15", "The two upstream selectors run as written; the menu's icon selector left with the menu."),
                new("Dialogs", "+140 / −130", "The edit dialogs became pages, the Yes/No dialogs one confirmation page, Save changes? a flyout."),
                new("Data access", "+245 / −150", "The services became server functions; five loaders give the view models their data."),
                new("Messenger", "+13 / −11", "WeakReferenceMessenger.Default became an injected IMessenger."),
                new("Composition", "+52 / −147", "Program.cs is the Aioros builder; App creates no window and builds no container."),
                new("Other", "+50 / −48", "Dispatcher posts removed, a few web stand-ins."),
            ]),
        new(
            "stylet",
            "Stylet.Avalonia samples",
            "Another MVVM framework",
            "Caliburn.Micro's model on Avalonia: screens and conductors, views found by name, {s:Action} binding a control to a view model method guarded by its Can property, and StyletIoC. Four samples in one app: Hello, HelloDialog, NavigationController and TabNavigation.",
            "sealoyal2018/Stylet.Avalonia at f56678d (0.5.1, MIT, a port of canton7/Stylet): Avalonia 11.0.2 on .NET 6 to 8",
            "https://github.com/sealoyal2018/Stylet.Avalonia",
            "Upstream Hello sample at f56678d on Avalonia 11.0.2 (.NET 8), drawn by Avalonia headless (Skia) at 1280 by 800 by bench/ports/captures.sh. Upstream enables Say Hello with an empty name; the port follows the CanSayHello guard.",
            "All 10 view model files, the sample's navigation controller, TabNavigation's two page views, 6 code-behinds and 87 of the library's 91 C# files.",
            "Hello, NavigationController and TabNavigation; {s:Action} works unchanged.",
            "All four samples; HelloDialog and Hello's message box open windows, so they are native only.",
            "5 of 5 web pages.",
            "The web version's Hello sample in Chromium at 1280 by 800.",
            "samples/Stylet",
            [
                new("Library on Avalonia 12", "4 files, +7 / −39", "One attribute gone from Avalonia 12 dropped, reflection bindings kept, packaging entries dropped."),
                new("Stylet fix and hosting", "2 files, +3 / −3", "An upstream bug fixed (every Add without a handler threw); IoC made public for other hosts."),
                new("Views", "12 files, +48 / −47", "Windows became user controls; each view names its view model and action target; the conductor's ActiveItem became an outlet."),
                new("Composition", "4 files, +126 / −0", "Microsoft's container instead of StyletIoC, a view manager without the name lookup, the navigation controller mapped to routes."),
                new("Pages", "10 files, +79 / −0", "A root layout linking the four samples and one page per sample."),
            ]),
        new(
            "reactiverouting",
            "ReactiveUI routing sample",
            "ReactiveUI navigation",
            "ReactiveUI's own Avalonia routing sample: a screen with a RoutingState, two routable view models, ReactiveCommands whose CanExecute follows the current view model, and a RoutedViewHost. The view models needed 2 lines for Aioros; the rest is where the views live and the composition.",
            "reactiveui/ReactiveUI.Samples, avalonia/suspension at 8e7ca27 (MIT): Avalonia 11.0.11, 14 files and 147 lines of view models",
            "https://github.com/reactiveui/ReactiveUI.Samples",
            "Upstream avalonia/suspension at 8e7ca27 on Avalonia 11.0.11, drawn by Avalonia headless (Skia) at 1280 by 800 by bench/ports/captures.sh, in its dark theme; the port renders in the light one.",
            "The view models, except 2 lines; the routes come from AddReactiveRoutes.",
            "/, /search and /login: Search and Login navigate, Back stays disabled as there is no back on the server.",
            "The same pages in a window.",
            "3 of 3 web pages.",
            "The web version's start page in Chromium at 1280 by 800.",
            "samples/ReactiveRouting",
            [
                new("View models", "2 lines", "Locator.Current.GetService<IScreen>() replaced by the constructor's screen: one process-wide locator would be shared by every user on a server."),
                new("Views", "4 lines per view, 3 files deleted", "Views moved to Pages/<route>/Page.axaml with x:DataType; the empty WhenActivated code-behinds deleted."),
                new("Layout", "9 lines", "The window becomes the layout: the RoutedViewHost becomes an outlet."),
                new("Composition", "+8 / −14, 1 file deleted", "Program.cs is the Aioros builder with three routes; the suspension driver (a file) is native only."),
                new("ReactiveUI 26", "10 lines", "System.Reactive usings and Unit replaced by ReactiveUI.Primitives and RxVoid: ReactiveUI's change, not Aioros's."),
                new("An upstream bug", "2 lines", "Router.Navigate.Execute(...) followed by .Subscribe(): Execute is cold, so the buttons never navigated, natively either."),
            ]),
    ];

    public static Port? Find(string slug) => All.FirstOrDefault(port => port.Slug == slug);
}
