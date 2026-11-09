using Avalonia;
using Avalonia.Controls;

namespace Aioros.Markup;

/// <summary>
/// Hints for the web, set in XAML as <c>a:Web.*</c> (<c>xmlns:a="https://aioros.dev"</c>) by an app or by a library's
/// control themes: Aioros reads them once when it traces a page for the web, and the native targets ignore them.
/// Accessibility semantics stay on Avalonia's <c>AutomationProperties</c>; <see cref="TagProperty"/> names the elements
/// Avalonia has no vocabulary for.
/// </summary>
public sealed partial class Web : AvaloniaObject
{
    /// <summary>A grid's columns stacked into one column when the viewport is narrower than this width, in pixels.</summary>
    public static readonly AttachedProperty<double> StackBelowProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("StackBelow", double.NaN);

    /// <summary>
    /// How far a grid splitter moves with an arrow key on the web, as a percentage of its grid's star tracks
    /// (react-resizable-panels' <c>keyboardResizeBy</c>), for a library whose splitter steps by percentages in its own code;
    /// without it the splitter moves by its <c>KeyboardIncrement</c> in pixels, as Avalonia's does.
    /// </summary>
    public static readonly AttachedProperty<double> ResizeStepProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("ResizeStep", double.NaN);

    /// <summary>
    /// The percentage of its grid's star tracks the track before a grid splitter collapses to when Enter is pressed on it
    /// on the web, and is restored from with Enter again (the WAI-ARIA window splitter, react-resizable-panels'
    /// <c>collapsedSize</c>), for a library whose panels collapse in its own code; without it Enter does nothing.
    /// </summary>
    public static readonly AttachedProperty<double> CollapsedSizeProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("CollapsedSize", double.NaN);

    /// <summary>
    /// The smallest percentage of its grid's star tracks a column or row definition keeps when a grid splitter moves on the
    /// web (react-resizable-panels' <c>minSize</c>), for a library whose panels keep percentages in its own code.
    /// </summary>
    public static readonly AttachedProperty<double> MinSizeProperty =
        AvaloniaProperty.RegisterAttached<Web, DefinitionBase, double>("MinSize", double.NaN);

    /// <summary>The largest percentage of its grid's star tracks a column or row definition takes when a grid splitter moves on the web (<c>maxSize</c>).</summary>
    public static readonly AttachedProperty<double> MaxSizeProperty =
        AvaloniaProperty.RegisterAttached<Web, DefinitionBase, double>("MaxSize", double.NaN);

    /// <summary>
    /// The width to height ratio the control keeps on the web (CSS <c>aspect-ratio</c>), for a library panel that keeps a
    /// ratio in its own layout code (shadcn's <c>AspectRatio</c>), so its height follows its width in any viewport.
    /// </summary>
    public static readonly AttachedProperty<double> AspectRatioProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("AspectRatio", double.NaN);

    /// <summary>
    /// How a library's control that arranges its children in its own code lays them out on the web: <c>Dock</c> writes a
    /// panel as a <c>DockPanel</c>, each child docked by its <c>DockPanel.Dock</c> and the last filling the rest (Aioros.Cosmo's
    /// <c>c:SidebarProvider</c>, its sidebar beside its inset); <c>Fill</c> says that a content control gives its content its
    /// own height, so a window-filling layout passes through it (Cosmo's <c>c:SidebarInset</c>). Any other value is ignored.
    /// </summary>
    public static readonly AttachedProperty<string?> LayoutProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("Layout");

    /// <summary>
    /// Set on a library's panel: the name of a <c>bool</c> property of its own that its code turns into its children's
    /// looks (Aioros.Cosmo's <c>c:SidebarProvider.IsOpen</c>), switched in the browser without a request by the buttons
    /// marked with <see cref="FlipsProperty"/> inside it, as shadcn/ui's sidebar switches its <c>data-state</c>. The panel
    /// is written in its value, with the looks of the other value ready under its <c>data-aioros-on</c> mark.
    /// </summary>
    public static readonly AttachedProperty<string?> FlagProperty =
        AvaloniaProperty.RegisterAttached<Web, Panel, string?>("Flag");

    /// <summary>
    /// Set by a library on a control of its own, usually in its theme: the names of the control's own properties, comma
    /// separated, that hold where its visitor is in it (Aioros.Cosmo's questionnaire's <c>CurrentIndex</c>). The web writes
    /// each value in a hidden input and restores it before the next post is bound and its button looked for, as a tab
    /// control's selected index travels (decided with the user on 2026-10-19); an <c>int</c>, a <c>double</c>, a
    /// <c>bool</c> (or <c>bool?</c>), an enum or a <c>string</c>, in its invariant text. The native targets keep the
    /// control itself, so they ignore it.
    /// </summary>
    public static readonly AttachedProperty<string?> StateProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("State");

    /// <summary>
    /// Set by a library on a list of its own that shows its choice through its template (Aioros.Cosmo's
    /// <c>c:Combobox</c>): the name of its <c>int</c> property holding the chosen item's index, <c>-1</c> for none
    /// (<c>SelectedIndex</c>). On the web the buttons inside it marked with <see cref="ChoosesProperty"/> choose in the
    /// browser with no request, as a native <c>&lt;select&gt;</c> does: the part marked <see cref="ShowsChoiceProperty"/>
    /// shows the chosen item as the template draws it for that index, each option is <c>aria-checked</c> while it is the
    /// chosen one and takes the looks its theme gives it under <c>:checked</c>, the popover holding the option closes, and
    /// the index is posted in a hidden field with the next post, or at once when the choice is bound two way; choosing the
    /// chosen item again chooses none when the list's <c>SelectionMode</c> has <c>Toggle</c>. The native targets ignore it.
    /// </summary>
    public static readonly AttachedProperty<string?> ChoiceProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("Choice");

    /// <summary>
    /// Set by a library on an option button inside a list with <see cref="ChoiceProperty"/>: the index the button chooses
    /// (its item's), so the web chooses it in the browser instead of posting; <c>-1</c>, the default, for a button that
    /// chooses nothing.
    /// </summary>
    public static readonly AttachedProperty<int> ChoosesProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, int>("Chooses", -1);

    /// <summary>
    /// Set in the template of a list with <see cref="ChoiceProperty"/> on the part that shows its choice (the trigger's
    /// text or placeholder): the web writes it once for each index the list can hold, so the browser shows the one chosen.
    /// </summary>
    public static readonly AttachedProperty<bool> ShowsChoiceProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, bool>("ShowsChoice");

    /// <summary>
    /// Set on a panel with <see cref="FlagProperty"/> (Aioros.Cosmo's <c>c:SidebarProvider</c> sets its <c>StateKey</c>):
    /// the cookie the browser keeps the flag's state in as it switches it, <c>name=true</c> or <c>name=false</c> for the
    /// whole site and seven days, as shadcn/ui's sidebar writes <c>sidebar_state</c>. The app reads it back from
    /// <c>PageRequest.Cookies</c> in a loader and binds the flag; the native targets ignore it.
    /// </summary>
    public static readonly AttachedProperty<string?> FlagCookieProperty =
        AvaloniaProperty.RegisterAttached<Web, Panel, string?>("FlagCookie");

    /// <summary>
    /// Set on a panel with <see cref="FlagProperty"/> (Aioros.Cosmo's <c>c:SidebarProvider</c> sets its
    /// <c>MobileBreakpoint</c>): the viewport width, in pixels, under which the panel is narrow on the web. The panel is
    /// traced in a window just narrower than it with its sheet (<see cref="SheetProperty"/>) open, and the looks that
    /// differ there are given under <c>@media (max-width)</c>; below it, the buttons that switch the panel open and close its
    /// sheet instead, as shadcn/ui's sidebar becomes a sheet under 768 px. The native targets ignore it.
    /// </summary>
    public static readonly AttachedProperty<double> BreakpointProperty =
        AvaloniaProperty.RegisterAttached<Web, Panel, double>("Breakpoint", double.NaN);

    /// <summary>
    /// Set on a control inside a panel with <see cref="BreakpointProperty"/> (Aioros.Cosmo's <c>c:Sidebar</c>): the name of
    /// the panel's <c>bool</c> property that opens the control as a sheet in a narrow window (<c>IsMobileOpen</c>). On the
    /// web the control is written in a <c>&lt;dialog&gt;</c> laid out in its place above the breakpoint and opened as a
    /// modal below it (the page behind it inert, Escape closing it), its open state kept in the browser.
    /// </summary>
    public static readonly AttachedProperty<string?> SheetProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("Sheet");

    /// <summary>
    /// Set on a button that switches the flag of the panel around it in its own code (Aioros.Cosmo's
    /// <c>c:SidebarTrigger</c>): the name of that panel's <see cref="FlagProperty"/>, which the button then switches in
    /// the browser instead of posting.
    /// </summary>
    public static readonly AttachedProperty<string?> FlipsProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("Flips");

    /// <summary>The <c>type</c> of a text box's <c>&lt;input&gt;</c> (<c>email</c>, <c>search</c>, <c>url</c>, ...).</summary>
    public static readonly AttachedProperty<string?> InputTypeProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("InputType");

    /// <summary>
    /// Set on a text box bound two way: on the web its text is posted as it changes, this many milliseconds after the
    /// visitor's last keystroke (a search box, a table's filter); 0 posts it only with the form, even when its binding has a
    /// <c>Delay</c>. Unset, a binding's own <c>Delay</c> does the same. Set on a grid splitter whose drags a library follows
    /// into a model bound two way (Aioros.Cosmo's resizable handles): its position is posted as a change this many
    /// milliseconds after a drag ends or a key moves it, instead of only with the form. The native targets ignore it, so
    /// their binding updates the model on every keystroke.
    /// </summary>
    public static readonly AttachedProperty<double> DelayProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("Delay", double.NaN);

    /// <summary>
    /// Set on a <c>ScrollViewer</c> (Aioros.Cosmo's message scroller opening at its newest message): on the web it opens at
    /// its end and keeps its end in view while its content grows there, until the visitor scrolls away, as shadcn/ui's
    /// <c>MessageScroller</c> follows its transcript (a scroll container whose scroll origin is its end,
    /// <c>flex-direction:column-reverse</c>). The native targets ignore it: the library's code scrolls there.
    /// </summary>
    public static readonly AttachedProperty<bool> FollowsEndProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, bool>("FollowsEnd");

    /// <summary>
    /// Set on a button: the name of a <c>ScrollViewer</c> in its name scope (a template part such as <c>PART_Viewport</c>)
    /// that a click scrolls to its end on the web, with no request, the button hidden while that viewer is at its end, as
    /// shadcn/ui's scroll-to-end button is. The native targets ignore it: the library's code scrolls there.
    /// </summary>
    public static readonly AttachedProperty<string?> ScrollsToEndProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("ScrollsToEnd");

    /// <summary>
    /// Set on a button (Aioros.Cosmo's <c>c:CodeBlockCopyButton</c>): the text a click copies to the visitor's clipboard
    /// on the web, with no request, the button marked <c>data-copied</c> for two seconds after, which takes the looks its
    /// theme gives it under <c>:copied</c>, as shadcn/ui's copy buttons show their check. Literal or bound. The native
    /// targets ignore it: the button's own code copies through its top level's clipboard.
    /// </summary>
    public static readonly AttachedProperty<string?> CopiesProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("Copies");

    /// <summary>The address the control links to: it is written as an <c>&lt;a href&gt;</c>, and navigates on the native targets.</summary>
    public static readonly AttachedProperty<string?> HrefProperty =
        AvaloniaProperty.RegisterAttached<Web, AvaloniaObject, string?>("Href");

    /// <summary>The link is current only on its own path, not on the paths below it (React Router's <c>end</c>).</summary>
    public static readonly AttachedProperty<bool> ExactProperty =
        AvaloniaProperty.RegisterAttached<Web, AvaloniaObject, bool>("Exact");

    /// <summary>The id of the popover a button opens and closes (<c>popovertarget</c>).</summary>
    public static readonly AttachedProperty<string?> TogglesProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("Toggles");

    /// <summary>The <c>id</c> of the control's element.</summary>
    public static readonly AttachedProperty<string?> IdProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("Id");

    /// <summary>The control is hidden when the viewport is at least this wide, in pixels.</summary>
    public static readonly AttachedProperty<double> HideAboveProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("HideAbove", double.NaN);

    /// <summary>The control is hidden when the viewport is narrower than this width, in pixels.</summary>
    public static readonly AttachedProperty<double> HideBelowProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("HideBelow", double.NaN);

    /// <summary>A split view's pane collapses when the viewport is narrower than this width, in pixels (900 by default).</summary>
    public static readonly AttachedProperty<double> CollapseBelowProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("CollapseBelow", 900);

    /// <summary>The control's <c>view-transition-name</c>, so a navigation animates it from one page to the next.</summary>
    public static readonly AttachedProperty<string?> TransitionNameProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("TransitionName");

    /// <summary>
    /// The HTML element the control is written as, for what Avalonia's automation properties cannot say: <c>kbd</c>,
    /// <c>code</c>, <c>pre</c>, <c>small</c>, <c>blockquote</c>, <c>ol</c>, <c>ul</c>, <c>li</c>, <c>p</c> (on a text
    /// only), and the sectioning elements <c>nav</c>, <c>header</c>, <c>footer</c>, <c>section</c>, <c>article</c>,
    /// <c>aside</c> and <c>main</c>. Any other name is ignored.
    /// </summary>
    public static readonly AttachedProperty<string?> TagProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("Tag");

    /// <summary>
    /// Set on a panel or a list (a single accordion): the expanders inside it open one at a time on the web, as an
    /// exclusive accordion (HTML's <c>&lt;details name&gt;</c>).
    /// </summary>
    public static readonly AttachedProperty<bool> ExclusiveProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, bool>("Exclusive");

    /// <summary>Set on a page's root: the page is live, its views updated from the server as their data changes.</summary>
    public static readonly AttachedProperty<bool> LiveProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, bool>("Live");

    /// <summary>
    /// Marks a link as "open in the desktop app": on the web, a click asks the browser to open the installed app on the
    /// current page through the app's scheme and follows the link (the download page) when no app takes it within
    /// about 1.5 seconds; without JavaScript the link is followed. In the installed app the link is hidden.
    /// </summary>
    public static readonly AttachedProperty<bool> OpenInAppProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, bool>("OpenInApp");

    /// <summary>
    /// Set in a list's control template, as a command menu's (cmdk's): <c>Input</c> on its text box, which then filters
    /// and ranks the list's items and their groups in the browser as the visitor types, by cmdk's <c>command-score</c> of
    /// each item's <c>TextSearch.Text</c> or text, with the listbox keys; <c>Empty</c> on the part the list shows when
    /// no item matches. Any other value is ignored.
    /// </summary>
    public static readonly AttachedProperty<string?> FilterProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("Filter");

    /// <summary>
    /// Set on a button with a flyout that a library opens on hover in its own code (Radix's NavigationMenu, Aioros.Cosmo's
    /// navigation menu trigger): on the web the flyout also opens once the pointer rests on the button this many
    /// milliseconds, at once when another such button of the same row has its flyout open, and a click still toggles it.
    /// </summary>
    public static readonly AttachedProperty<double> HoverOpenDelayProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("HoverOpenDelay", double.NaN);

    /// <summary>
    /// How many milliseconds after the pointer leaves a button opened on hover (<see cref="HoverOpenDelayProperty"/>) and
    /// its flyout the flyout closes on the web; 150 by default, Radix's NavigationMenu's.
    /// </summary>
    public static readonly AttachedProperty<double> HoverCloseDelayProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("HoverCloseDelay", 150);

    /// <summary>Gets <see cref="StackBelowProperty"/>.</summary>
    /// <param name="control">The grid.</param>
    /// <returns>The width, or NaN.</returns>
    public static double GetStackBelow(Control control) => control.GetValue(StackBelowProperty);

    /// <summary>Sets <see cref="StackBelowProperty"/>.</summary>
    /// <param name="control">The grid.</param>
    /// <param name="value">The width.</param>
    public static void SetStackBelow(Control control, double value) => control.SetValue(StackBelowProperty, value);

    /// <summary>Gets <see cref="ResizeStepProperty"/>.</summary>
    public static double GetResizeStep(Control control) => control.GetValue(ResizeStepProperty);

    /// <summary>Sets <see cref="ResizeStepProperty"/>.</summary>
    public static void SetResizeStep(Control control, double value) => control.SetValue(ResizeStepProperty, value);

    /// <summary>Gets <see cref="CollapsedSizeProperty"/>.</summary>
    public static double GetCollapsedSize(Control control) => control.GetValue(CollapsedSizeProperty);

    /// <summary>Sets <see cref="CollapsedSizeProperty"/>.</summary>
    public static void SetCollapsedSize(Control control, double value) => control.SetValue(CollapsedSizeProperty, value);

    /// <summary>Gets <see cref="MinSizeProperty"/>.</summary>
    public static double GetMinSize(DefinitionBase definition) => definition.GetValue(MinSizeProperty);

    /// <summary>Sets <see cref="MinSizeProperty"/>.</summary>
    public static void SetMinSize(DefinitionBase definition, double value) => definition.SetValue(MinSizeProperty, value);

    /// <summary>Gets <see cref="MaxSizeProperty"/>.</summary>
    public static double GetMaxSize(DefinitionBase definition) => definition.GetValue(MaxSizeProperty);

    /// <summary>Sets <see cref="MaxSizeProperty"/>.</summary>
    public static void SetMaxSize(DefinitionBase definition, double value) => definition.SetValue(MaxSizeProperty, value);

    /// <summary>Gets <see cref="AspectRatioProperty"/>.</summary>
    public static double GetAspectRatio(Control control) => control.GetValue(AspectRatioProperty);

    /// <summary>Sets <see cref="AspectRatioProperty"/>.</summary>
    public static void SetAspectRatio(Control control, double value) => control.SetValue(AspectRatioProperty, value);

    /// <summary>Gets <see cref="LayoutProperty"/>.</summary>
    public static string? GetLayout(Control control) => control.GetValue(LayoutProperty);

    /// <summary>Sets <see cref="LayoutProperty"/>.</summary>
    public static void SetLayout(Control control, string? value) => control.SetValue(LayoutProperty, value);

    /// <summary>Gets <see cref="FollowsEndProperty"/>.</summary>
    public static bool GetFollowsEnd(Control control) => control.GetValue(FollowsEndProperty);

    /// <summary>Sets <see cref="FollowsEndProperty"/>.</summary>
    public static void SetFollowsEnd(Control control, bool value) => control.SetValue(FollowsEndProperty, value);

    /// <summary>Gets <see cref="ScrollsToEndProperty"/>.</summary>
    public static string? GetScrollsToEnd(Control control) => control.GetValue(ScrollsToEndProperty);

    /// <summary>Sets <see cref="ScrollsToEndProperty"/>.</summary>
    public static void SetScrollsToEnd(Control control, string? value) => control.SetValue(ScrollsToEndProperty, value);

    /// <summary>Gets <see cref="CopiesProperty"/>.</summary>
    public static string? GetCopies(Control control) => control.GetValue(CopiesProperty);

    /// <summary>Sets <see cref="CopiesProperty"/>.</summary>
    public static void SetCopies(Control control, string? value) => control.SetValue(CopiesProperty, value);

    /// <summary>Gets <see cref="BreakpointProperty"/>.</summary>
    public static double GetBreakpoint(Panel panel) => panel.GetValue(BreakpointProperty);

    /// <summary>Sets <see cref="BreakpointProperty"/>.</summary>
    public static void SetBreakpoint(Panel panel, double value) => panel.SetValue(BreakpointProperty, value);

    /// <summary>Gets <see cref="SheetProperty"/>.</summary>
    public static string? GetSheet(Control control) => control.GetValue(SheetProperty);

    /// <summary>Sets <see cref="SheetProperty"/>.</summary>
    public static void SetSheet(Control control, string? value) => control.SetValue(SheetProperty, value);

    /// <summary>Gets <see cref="FlagCookieProperty"/>.</summary>
    public static string? GetFlagCookie(Panel panel) => panel.GetValue(FlagCookieProperty);

    /// <summary>Sets <see cref="FlagCookieProperty"/>.</summary>
    public static void SetFlagCookie(Panel panel, string? value) => panel.SetValue(FlagCookieProperty, value);

    /// <summary>Gets <see cref="StateProperty"/>.</summary>
    /// <param name="control">The library's control.</param>
    /// <returns>The names of its properties that travel across posts.</returns>
    public static string? GetState(Control control) => control.GetValue(StateProperty);

    /// <summary>Sets <see cref="StateProperty"/>.</summary>
    /// <param name="control">The library's control.</param>
    /// <param name="value">The names of its properties that travel across posts, comma separated.</param>
    public static void SetState(Control control, string? value) => control.SetValue(StateProperty, value);

    /// <summary>Gets <see cref="ChoiceProperty"/>.</summary>
    /// <param name="control">The library's list.</param>
    /// <returns>The name of its property holding the chosen index.</returns>
    public static string? GetChoice(Control control) => control.GetValue(ChoiceProperty);

    /// <summary>Sets <see cref="ChoiceProperty"/>.</summary>
    /// <param name="control">The library's list.</param>
    /// <param name="value">The name of its <c>int</c> property holding the chosen index.</param>
    public static void SetChoice(Control control, string? value) => control.SetValue(ChoiceProperty, value);

    /// <summary>Gets <see cref="ChoosesProperty"/>.</summary>
    /// <param name="control">The option button.</param>
    /// <returns>The index it chooses, or -1.</returns>
    public static int GetChooses(Control control) => control.GetValue(ChoosesProperty);

    /// <summary>Sets <see cref="ChoosesProperty"/>.</summary>
    /// <param name="control">The option button.</param>
    /// <param name="value">The index it chooses, or -1.</param>
    public static void SetChooses(Control control, int value) => control.SetValue(ChoosesProperty, value);

    /// <summary>Gets <see cref="ShowsChoiceProperty"/>.</summary>
    /// <param name="control">The template's part.</param>
    /// <returns>Whether it shows the list's choice.</returns>
    public static bool GetShowsChoice(Control control) => control.GetValue(ShowsChoiceProperty);

    /// <summary>Sets <see cref="ShowsChoiceProperty"/>.</summary>
    /// <param name="control">The template's part.</param>
    /// <param name="value">Whether it shows the list's choice.</param>
    public static void SetShowsChoice(Control control, bool value) => control.SetValue(ShowsChoiceProperty, value);

    /// <summary>Gets <see cref="FlagProperty"/>.</summary>
    public static string? GetFlag(Panel panel) => panel.GetValue(FlagProperty);

    /// <summary>Sets <see cref="FlagProperty"/>.</summary>
    public static void SetFlag(Panel panel, string? value) => panel.SetValue(FlagProperty, value);

    /// <summary>Gets <see cref="FlipsProperty"/>.</summary>
    public static string? GetFlips(Control control) => control.GetValue(FlipsProperty);

    /// <summary>Sets <see cref="FlipsProperty"/>.</summary>
    public static void SetFlips(Control control, string? value) => control.SetValue(FlipsProperty, value);

    /// <summary>Gets <see cref="InputTypeProperty"/>.</summary>
    /// <param name="control">The text box.</param>
    /// <returns>The input type, or null.</returns>
    public static string? GetInputType(Control control) => control.GetValue(InputTypeProperty);

    /// <summary>Sets <see cref="InputTypeProperty"/>.</summary>
    /// <param name="control">The text box.</param>
    /// <param name="value">The input type.</param>
    public static void SetInputType(Control control, string? value) => control.SetValue(InputTypeProperty, value);

    /// <summary>Gets <see cref="DelayProperty"/>.</summary>
    /// <param name="control">The text box.</param>
    /// <returns>The delay in milliseconds, or <see cref="double.NaN"/> when it is not set.</returns>
    public static double GetDelay(Control control) => control.GetValue(DelayProperty);

    /// <summary>Sets <see cref="DelayProperty"/>.</summary>
    /// <param name="control">The text box.</param>
    /// <param name="value">The delay in milliseconds.</param>
    public static void SetDelay(Control control, double value) => control.SetValue(DelayProperty, value);

    /// <summary>Gets <see cref="HrefProperty"/>.</summary>
    /// <param name="target">The link.</param>
    /// <returns>The address, or null.</returns>
    public static string? GetHref(AvaloniaObject target) => target.GetValue(HrefProperty);

    /// <summary>Sets <see cref="HrefProperty"/>.</summary>
    /// <param name="target">The link.</param>
    /// <param name="value">The address.</param>
    public static void SetHref(AvaloniaObject target, string? value) => target.SetValue(HrefProperty, value);

    /// <summary>Gets <see cref="ExactProperty"/>.</summary>
    /// <param name="target">The link.</param>
    /// <returns>Whether the link is current only on its own path.</returns>
    public static bool GetExact(AvaloniaObject target) => target.GetValue(ExactProperty);

    /// <summary>Sets <see cref="ExactProperty"/>.</summary>
    /// <param name="target">The link.</param>
    /// <param name="value">Whether the link is current only on its own path.</param>
    public static void SetExact(AvaloniaObject target, bool value) => target.SetValue(ExactProperty, value);

    /// <summary>Gets <see cref="TogglesProperty"/>.</summary>
    /// <param name="control">The button.</param>
    /// <returns>The popover's id, or null.</returns>
    public static string? GetToggles(Control control) => control.GetValue(TogglesProperty);

    /// <summary>Sets <see cref="TogglesProperty"/>.</summary>
    /// <param name="control">The button.</param>
    /// <param name="value">The popover's id.</param>
    public static void SetToggles(Control control, string? value) => control.SetValue(TogglesProperty, value);

    /// <summary>Gets <see cref="IdProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The id, or null.</returns>
    public static string? GetId(Control control) => control.GetValue(IdProperty);

    /// <summary>Sets <see cref="IdProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <param name="value">The id.</param>
    public static void SetId(Control control, string? value) => control.SetValue(IdProperty, value);

    /// <summary>Gets <see cref="HideAboveProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The width, or NaN.</returns>
    public static double GetHideAbove(Control control) => control.GetValue(HideAboveProperty);

    /// <summary>Sets <see cref="HideAboveProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <param name="value">The width.</param>
    public static void SetHideAbove(Control control, double value) => control.SetValue(HideAboveProperty, value);

    /// <summary>Gets <see cref="HideBelowProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The width, or NaN.</returns>
    public static double GetHideBelow(Control control) => control.GetValue(HideBelowProperty);

    /// <summary>Sets <see cref="HideBelowProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <param name="value">The width.</param>
    public static void SetHideBelow(Control control, double value) => control.SetValue(HideBelowProperty, value);

    /// <summary>Gets <see cref="CollapseBelowProperty"/>.</summary>
    /// <param name="control">The split view.</param>
    /// <returns>The width.</returns>
    public static double GetCollapseBelow(Control control) => control.GetValue(CollapseBelowProperty);

    /// <summary>Sets <see cref="CollapseBelowProperty"/>.</summary>
    /// <param name="control">The split view.</param>
    /// <param name="value">The width.</param>
    public static void SetCollapseBelow(Control control, double value) => control.SetValue(CollapseBelowProperty, value);

    /// <summary>Gets <see cref="TransitionNameProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The name, or null.</returns>
    public static string? GetTransitionName(Control control) => control.GetValue(TransitionNameProperty);

    /// <summary>Sets <see cref="TransitionNameProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <param name="value">The name.</param>
    public static void SetTransitionName(Control control, string? value) => control.SetValue(TransitionNameProperty, value);

    /// <summary>Gets <see cref="TagProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The element's name, or null.</returns>
    public static string? GetTag(Control control) => control.GetValue(TagProperty);

    /// <summary>Sets <see cref="TagProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <param name="value">The element's name.</param>
    public static void SetTag(Control control, string? value) => control.SetValue(TagProperty, value);

    /// <summary>Gets <see cref="LiveProperty"/>.</summary>
    /// <param name="control">The page's root.</param>
    /// <returns>Whether the page is live.</returns>
    public static bool GetLive(Control control) => control.GetValue(LiveProperty);

    /// <summary>Sets <see cref="LiveProperty"/>.</summary>
    /// <param name="control">The page's root.</param>
    /// <param name="value">Whether the page is live.</param>
    public static void SetLive(Control control, bool value) => control.SetValue(LiveProperty, value);

    /// <summary>Gets <see cref="OpenInAppProperty"/>.</summary>
    /// <param name="control">The link.</param>
    /// <returns>Whether the link opens the installed app.</returns>
    public static bool GetOpenInApp(Control control) => control.GetValue(OpenInAppProperty);

    /// <summary>Sets <see cref="OpenInAppProperty"/>.</summary>
    /// <param name="control">The link.</param>
    /// <param name="value">Whether the link opens the installed app.</param>
    public static void SetOpenInApp(Control control, bool value) => control.SetValue(OpenInAppProperty, value);

    /// <summary>Gets <see cref="ExclusiveProperty"/>.</summary>
    /// <param name="control">The panel or list.</param>
    /// <returns>Whether its expanders open one at a time.</returns>
    public static bool GetExclusive(Control control) => control.GetValue(ExclusiveProperty);

    /// <summary>Sets <see cref="ExclusiveProperty"/>.</summary>
    /// <param name="control">The panel or list.</param>
    /// <param name="value">Whether its expanders open one at a time.</param>
    public static void SetExclusive(Control control, bool value) => control.SetValue(ExclusiveProperty, value);

    /// <summary>Gets <see cref="FilterProperty"/>.</summary>
    /// <param name="control">The list's text box, or the part shown when nothing matches.</param>
    /// <returns><c>Input</c>, <c>Empty</c>, or null.</returns>
    public static string? GetFilter(Control control) => control.GetValue(FilterProperty);

    /// <summary>Sets <see cref="FilterProperty"/>.</summary>
    /// <param name="control">The list's text box, or the part shown when nothing matches.</param>
    /// <param name="value"><c>Input</c> or <c>Empty</c>.</param>
    public static void SetFilter(Control control, string? value) => control.SetValue(FilterProperty, value);

    /// <summary>Gets <see cref="HoverOpenDelayProperty"/>.</summary>
    /// <param name="control">The button.</param>
    /// <returns>The delay in milliseconds, or NaN when only a click opens its flyout.</returns>
    public static double GetHoverOpenDelay(Control control) => control.GetValue(HoverOpenDelayProperty);

    /// <summary>Sets <see cref="HoverOpenDelayProperty"/>.</summary>
    /// <param name="control">The button.</param>
    /// <param name="value">The delay in milliseconds.</param>
    public static void SetHoverOpenDelay(Control control, double value) => control.SetValue(HoverOpenDelayProperty, value);

    /// <summary>Gets <see cref="HoverCloseDelayProperty"/>.</summary>
    /// <param name="control">The button.</param>
    /// <returns>The delay in milliseconds.</returns>
    public static double GetHoverCloseDelay(Control control) => control.GetValue(HoverCloseDelayProperty);

    /// <summary>Sets <see cref="HoverCloseDelayProperty"/>.</summary>
    /// <param name="control">The button.</param>
    /// <param name="value">The delay in milliseconds.</param>
    public static void SetHoverCloseDelay(Control control, double value) => control.SetValue(HoverCloseDelayProperty, value);
}
