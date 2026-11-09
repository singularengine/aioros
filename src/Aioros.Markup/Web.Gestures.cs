using Avalonia;
using Avalonia.Controls;

namespace Aioros.Markup;

/// <summary>The hints of a library's gestures: what a visitor swipes away, how a stack of notices lays itself out and the keys a region answers.</summary>
public sealed partial class Web
{
    /// <summary>
    /// Set on a notice's card (a window notification manager's card) or on a library dialog's surface (Aioros.Cosmo's
    /// drawer): the directions, comma separated (<c>Up</c>, <c>Down</c>, <c>Left</c>, <c>Right</c>), the visitor drags it
    /// away in on the web. It follows the pointer, dampened against those directions, and is let go past
    /// <see cref="SwipeDistanceProperty"/> or faster than <see cref="SwipeVelocityProperty"/>: a card then leaves the way it
    /// went (sonner's swipe), a dialog closes from where it stands (vaul's drag); otherwise it moves back. The native
    /// targets ignore it.
    /// </summary>
    public static readonly AttachedProperty<string?> SwipeProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("Swipe");

    /// <summary>
    /// How far a swiped element (<see cref="SwipeProperty"/>) must go to be let go away: pixels (45 by default, sonner's
    /// threshold), or, below 1, a fraction of its size along the swipe (vaul's 0.25).
    /// </summary>
    public static readonly AttachedProperty<double> SwipeDistanceProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("SwipeDistance", 45);

    /// <summary>
    /// How fast, in pixels per millisecond over the whole drag, a swiped element (<see cref="SwipeProperty"/>) is let go
    /// away however far it went: 0.11 by default (sonner's), vaul's 0.4.
    /// </summary>
    public static readonly AttachedProperty<double> SwipeVelocityProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("SwipeVelocity", 0.11);

    /// <summary>
    /// Set on a window notification manager's panel that stacks its cards as sonner's toaster does (Aioros.Cosmo's
    /// <c>c:ToastDeck</c>): the gap between cards in pixels. On the web the browser keeps the stack: collapsed, the cards
    /// behind the newest stand this far apart each, shrunk by <see cref="DeckScaleProperty"/> each, as tall as the newest,
    /// with the looks the panel's <see cref="DeckBehindProperty"/> class gives them, and hidden past
    /// <see cref="DeckVisibleProperty"/>; under the pointer (or always with <see cref="DeckExpandedProperty"/>) they stand
    /// this far apart at their own heights. The cards' time stops while the stack is expanded, pressed or the page
    /// hidden, a later answer's cards join the stack in front, and the stack closes up as cards leave. NaN, the default,
    /// for none. The native targets ignore it.
    /// </summary>
    public static readonly AttachedProperty<double> DeckProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("Deck", double.NaN);

    /// <summary>How many cards of a deck (<see cref="DeckProperty"/>) are shown: 3 by default, sonner's.</summary>
    public static readonly AttachedProperty<int> DeckVisibleProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, int>("DeckVisible", 3);

    /// <summary>How much smaller each card behind the newest is in a collapsed deck (<see cref="DeckProperty"/>): 0.05, sonner's.</summary>
    public static readonly AttachedProperty<double> DeckScaleProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("DeckScale", 0.05);

    /// <summary>Whether a deck (<see cref="DeckProperty"/>) always stands expanded (sonner's <c>expand</c>).</summary>
    public static readonly AttachedProperty<bool> DeckExpandedProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, bool>("DeckExpanded");

    /// <summary>
    /// The class a deck (<see cref="DeckProperty"/>) gives a card behind the newest while collapsed (Aioros.Cosmo's
    /// <c>behind</c>, which fades the card's content): the web learns the opacity each part of a card takes with it and
    /// gives it to the cards behind in the browser.
    /// </summary>
    public static readonly AttachedProperty<string?> DeckBehindProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("DeckBehind");

    /// <summary>Gets <see cref="SwipeProperty"/>.</summary>
    /// <param name="control">The card or surface.</param>
    /// <returns>The directions, or null.</returns>
    public static string? GetSwipe(Control control) => control.GetValue(SwipeProperty);

    /// <summary>Sets <see cref="SwipeProperty"/>.</summary>
    /// <param name="control">The card or surface.</param>
    /// <param name="value">The directions.</param>
    public static void SetSwipe(Control control, string? value) => control.SetValue(SwipeProperty, value);

    /// <summary>Gets <see cref="SwipeDistanceProperty"/>.</summary>
    /// <param name="control">The card or surface.</param>
    /// <returns>The distance.</returns>
    public static double GetSwipeDistance(Control control) => control.GetValue(SwipeDistanceProperty);

    /// <summary>Sets <see cref="SwipeDistanceProperty"/>.</summary>
    /// <param name="control">The card or surface.</param>
    /// <param name="value">The distance.</param>
    public static void SetSwipeDistance(Control control, double value) => control.SetValue(SwipeDistanceProperty, value);

    /// <summary>Gets <see cref="SwipeVelocityProperty"/>.</summary>
    /// <param name="control">The card or surface.</param>
    /// <returns>The velocity.</returns>
    public static double GetSwipeVelocity(Control control) => control.GetValue(SwipeVelocityProperty);

    /// <summary>Sets <see cref="SwipeVelocityProperty"/>.</summary>
    /// <param name="control">The card or surface.</param>
    /// <param name="value">The velocity.</param>
    public static void SetSwipeVelocity(Control control, double value) => control.SetValue(SwipeVelocityProperty, value);

    /// <summary>Gets <see cref="DeckProperty"/>.</summary>
    /// <param name="control">The panel.</param>
    /// <returns>The gap, or NaN.</returns>
    public static double GetDeck(Control control) => control.GetValue(DeckProperty);

    /// <summary>Sets <see cref="DeckProperty"/>.</summary>
    /// <param name="control">The panel.</param>
    /// <param name="value">The gap.</param>
    public static void SetDeck(Control control, double value) => control.SetValue(DeckProperty, value);

    /// <summary>Gets <see cref="DeckVisibleProperty"/>.</summary>
    /// <param name="control">The panel.</param>
    /// <returns>How many cards are shown.</returns>
    public static int GetDeckVisible(Control control) => control.GetValue(DeckVisibleProperty);

    /// <summary>Sets <see cref="DeckVisibleProperty"/>.</summary>
    /// <param name="control">The panel.</param>
    /// <param name="value">How many cards are shown.</param>
    public static void SetDeckVisible(Control control, int value) => control.SetValue(DeckVisibleProperty, value);

    /// <summary>Gets <see cref="DeckScaleProperty"/>.</summary>
    /// <param name="control">The panel.</param>
    /// <returns>The step.</returns>
    public static double GetDeckScale(Control control) => control.GetValue(DeckScaleProperty);

    /// <summary>Sets <see cref="DeckScaleProperty"/>.</summary>
    /// <param name="control">The panel.</param>
    /// <param name="value">The step.</param>
    public static void SetDeckScale(Control control, double value) => control.SetValue(DeckScaleProperty, value);

    /// <summary>Gets <see cref="DeckExpandedProperty"/>.</summary>
    /// <param name="control">The panel.</param>
    /// <returns>Whether the deck stands expanded.</returns>
    public static bool GetDeckExpanded(Control control) => control.GetValue(DeckExpandedProperty);

    /// <summary>Sets <see cref="DeckExpandedProperty"/>.</summary>
    /// <param name="control">The panel.</param>
    /// <param name="value">Whether the deck stands expanded.</param>
    public static void SetDeckExpanded(Control control, bool value) => control.SetValue(DeckExpandedProperty, value);

    /// <summary>Gets <see cref="DeckBehindProperty"/>.</summary>
    /// <param name="control">The panel.</param>
    /// <returns>The class, or null.</returns>
    public static string? GetDeckBehind(Control control) => control.GetValue(DeckBehindProperty);

    /// <summary>Sets <see cref="DeckBehindProperty"/>.</summary>
    /// <param name="control">The panel.</param>
    /// <param name="value">The class.</param>
    public static void SetDeckBehind(Control control, string? value) => control.SetValue(DeckBehindProperty, value);

    /// <summary>
    /// The control is a region whose buttons answer their <see cref="KeysProperty"/> on the web while the focus is inside
    /// it (a questionnaire answering Ctrl+Enter and the arrows), as a library's control answers its own <c>KeyDown</c> on
    /// the native targets, which ignore it.
    /// </summary>
    public static readonly AttachedProperty<bool> KeyScopeProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, bool>("KeyScope");

    /// <summary>
    /// The keys, space separated in Avalonia's gesture syntax (<c>Ctrl+Enter Right</c>), that click the button on the web
    /// while the focus is inside the region around it marked with <see cref="KeyScopeProperty"/>, when the button is shown
    /// and enabled. A key without Ctrl, Meta or Alt is ignored in a text field, an arrow on a radio button (the browser moves
    /// its group's choice), and Enter is taken from a choice or a text field, the browser's implicit submission prevented, but
    /// answers only from a checked choice or a filled text field (shadcn/ui's questionnaire).
    /// The native targets ignore it.
    /// </summary>
    public static readonly AttachedProperty<string?> KeysProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("Keys");

    /// <summary>Gets <see cref="KeyScopeProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <returns>Whether its buttons answer their keys while the focus is inside it.</returns>
    public static bool GetKeyScope(Control control) => control.GetValue(KeyScopeProperty);

    /// <summary>Sets <see cref="KeyScopeProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <param name="value">Whether its buttons answer their keys while the focus is inside it.</param>
    public static void SetKeyScope(Control control, bool value) => control.SetValue(KeyScopeProperty, value);

    /// <summary>Gets <see cref="KeysProperty"/>.</summary>
    /// <param name="control">The button.</param>
    /// <returns>The keys that click it on the web.</returns>
    public static string? GetKeys(Control control) => control.GetValue(KeysProperty);

    /// <summary>Sets <see cref="KeysProperty"/>.</summary>
    /// <param name="control">The button.</param>
    /// <param name="value">The keys that click it on the web.</param>
    public static void SetKeys(Control control, string? value) => control.SetValue(KeysProperty, value);
}
