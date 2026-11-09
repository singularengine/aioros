using System.Collections.Immutable;
using System.Reflection;
using Aioros.Pages;

namespace Aioros.Hosting;

/// <summary>
/// The app as its generated entry point hands it to the host. The application type is a function, so creating the
/// record loads no Avalonia type.
/// </summary>
public sealed record AiorosApp(ImmutableArray<PageDefinition> Pages, Func<Type>? ApplicationType, Assembly Assembly, string ProjectDirectory, PageDefinition? NotFound = null, PageDefinition? Error = null)
{
    public ImmutableArray<Aioros.Runtime.Functions.ServerFunctionGroup> Functions { get; init; } = [];

    public ImmutableArray<Aioros.Runtime.Pages.NavigationInterface> Navigators { get; init; } = [];

    /// <summary>The typed getters and setters generated for the members the app's plans read.</summary>
    public ImmutableArray<Aioros.Runtime.Rendering.MemberAccessor> Accessors { get; init; } = [];

    /// <summary>The generated accessors of Avalonia types plans read, created only when a value of one is read.</summary>
    public ImmutableArray<Aioros.Runtime.Rendering.DeferredAccessors> DeferredAccessors { get; init; } = [];

    /// <summary>The types plans name, generated so a plan finds them without <c>Type.GetType</c>.</summary>
    public ImmutableArray<Aioros.Runtime.Slots.PlanType> Types { get; init; } = [];

    /// <summary>How each command type the view models expose is run and awaited, generated instead of found by reflection.</summary>
    public ImmutableArray<Aioros.Runtime.Slots.CommandShape> Commands { get; init; } = [];

    /// <summary>The app's value converters and how to run them, generated instead of rebuilt by reflection.</summary>
    public Aioros.Runtime.Slots.ConverterCode? Converters { get; init; }

    /// <summary>The app's template selectors, rebuilt with the templates a plan names and run by generated code instead of reflection.</summary>
    public ImmutableArray<Aioros.Runtime.Slots.SelectorCode> Selectors { get; init; } = [];

    /// <summary>The slot writers the app and its libraries declare, by name, generated from their <c>SlotWriterAttribute&lt;T&gt;</c>s.</summary>
    public ImmutableArray<Aioros.Runtime.Rendering.SlotWriterEntry> SlotWriters { get; init; } = [];

    /// <summary>The icons of the icon sets the app and its libraries declare, registered for the <c>Icon</c> control when the host starts.</summary>
    public ImmutableArray<KeyValuePair<string, string>> Icons { get; init; } = [];

    /// <summary>The server origin baked in at build time (<c>AiorosServer</c>, set by <c>aioros build --server</c>), which the installed app works against.</summary>
    public string? Server { get; init; }

    /// <summary>The app's stand-in for native-only pages on the web (<c>Pages/NativeOnly.axaml</c>), when it has one.</summary>
    public PageDefinition? NativeOnly { get; init; }

    /// <summary>
    /// The app's URL scheme baked in at build time (<c>AiorosScheme</c>): the website's "open in the desktop app" links
    /// open <c>{scheme}://open?path=...</c>, and the desktop app is single-instance and follows those deep links.
    /// </summary>
    public string? Scheme { get; init; }
}
