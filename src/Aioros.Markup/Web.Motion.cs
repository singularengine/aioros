using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Aioros.Markup;

/// <summary>The hints of a library's motion: where its controls are written rather than shown, and how its overlays slide.</summary>
public sealed partial class Web
{
    /// <summary>
    /// Set by Aioros on the window its engine realizes a page in, and inherited by every control of the page: the tree is
    /// written as HTML, not shown on a screen (as Avalonia's <c>Design.IsDesignMode</c> tells a control it is in a
    /// designer). A library skips there what only a screen needs: an exit that keeps a closed overlay in the tree until it
    /// has played, which the page would write as a second overlay. The native targets never set it.
    /// </summary>
    public static readonly AttachedProperty<bool> IsWrittenProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, bool>("IsWritten", inherits: true);

    /// <summary>
    /// Set on buttons opened on hover (<see cref="HoverOpenDelayProperty"/>) whose flyouts slide between each other as
    /// Radix's NavigationMenu content does (<c>data-motion</c>): when the flyout of one such button of the same row
    /// replaces another's, the new one enters this many pixels away on the side of the button it replaced (from the end
    /// when it is after it, Radix's <c>from-end</c>) and the old one leaves as far toward the other side (<c>to-start</c>),
    /// both with their own entrance and exit. NaN, the default, for none. The native targets ignore it.
    /// </summary>
    public static readonly AttachedProperty<double> SwitchOffsetProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("SwitchOffset", double.NaN);

    /// <summary>
    /// The control plays its style animations when it first scrolls into view rather than when its page opens, once (Framer
    /// Motion's <c>whileInView</c> with <c>once</c>): an entrance that waits for the visitor to reach it. On the web the
    /// framework's script holds a control below the viewport at its animations' start and plays them as it enters, and
    /// without the script they play as the page opens; on the desktop they play again when the control first enters its
    /// scroll viewer's view.
    /// </summary>
    public static readonly AttachedProperty<bool> RevealProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, bool>("Reveal");

    /// <summary>
    /// A text whose foreground is a gradient sweeps it across its glyphs, one whole gradient every this many seconds,
    /// without end: the gradient is drawn twice as wide as the text and repeats, so a gradient whose ends match moves
    /// seamlessly (the animated gradient headings of landing pages). NaN, the default, for none; it does nothing on a
    /// solid foreground. Still for a visitor who prefers reduced motion on the web.
    /// </summary>
    public static readonly AttachedProperty<double> SweepProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("Sweep", double.NaN);

    /// <summary>Gets <see cref="SweepProperty"/>.</summary>
    /// <param name="control">The text.</param>
    /// <returns>The seconds one sweep takes, or NaN.</returns>
    public static double GetSweep(Control control) => control.GetValue(SweepProperty);

    /// <summary>Sets <see cref="SweepProperty"/>.</summary>
    /// <param name="control">The text.</param>
    /// <param name="value">The seconds one sweep takes.</param>
    public static void SetSweep(Control control, double value) => control.SetValue(SweepProperty, value);

    /// <summary>
    /// A text holding a number counts up to it from zero over this many seconds, once, when it first comes into view
    /// (the counting figures of landing pages): each step is written in the text's own format, its prefix, suffix,
    /// grouping and decimals kept, and it ends on exactly the text. On the web the page holds the value itself, which a
    /// visitor without script reads, and a visitor who prefers reduced motion sees the value at once. NaN, the default,
    /// for none.
    /// </summary>
    public static readonly AttachedProperty<double> CountUpProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("CountUp", double.NaN);

    /// <summary>Gets <see cref="CountUpProperty"/>.</summary>
    /// <param name="control">The text.</param>
    /// <returns>The seconds the count takes, or NaN.</returns>
    public static double GetCountUp(Control control) => control.GetValue(CountUpProperty);

    /// <summary>Sets <see cref="CountUpProperty"/>.</summary>
    /// <param name="control">The text.</param>
    /// <param name="value">The seconds the count takes.</param>
    public static void SetCountUp(Control control, double value) => control.SetValue(CountUpProperty, value);

    /// <summary>
    /// A horizontal panel holding its items twice moves left by half its width every this many seconds, without end, so
    /// the second copy takes the first's place seamlessly (a marquee of logos); on the web it pauses while the pointer is
    /// over it and stands still for a visitor who prefers reduced motion. Its parent clips it. NaN, the default, for none.
    /// </summary>
    public static readonly AttachedProperty<double> MarqueeProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("Marquee", double.NaN);

    /// <summary>Gets <see cref="MarqueeProperty"/>.</summary>
    /// <param name="control">The panel.</param>
    /// <returns>The seconds one loop takes, or NaN.</returns>
    public static double GetMarquee(Control control) => control.GetValue(MarqueeProperty);

    /// <summary>Sets <see cref="MarqueeProperty"/>.</summary>
    /// <param name="control">The panel.</param>
    /// <param name="value">The seconds one loop takes.</param>
    public static void SetMarquee(Control control, double value) => control.SetValue(MarqueeProperty, value);

    /// <summary>
    /// The control glows with this color where the pointer is while the pointer is over it (the pointer spotlights of
    /// landing pages' cards): a radial gradient of the color, fading to transparent, over the control and clipped to its
    /// corners, which takes no input. Null, the default, for none.
    /// </summary>
    public static readonly AttachedProperty<IBrush?> SpotlightProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, IBrush?>("Spotlight");

    /// <summary>Gets <see cref="SpotlightProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The glow's color, or null.</returns>
    public static IBrush? GetSpotlight(Control control) => control.GetValue(SpotlightProperty);

    /// <summary>Sets <see cref="SpotlightProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <param name="value">The glow's color.</param>
    public static void SetSpotlight(Control control, IBrush? value) => control.SetValue(SpotlightProperty, value);

    /// <summary>
    /// A layer that moves against the scroll by this share of its distance from the viewport's middle: 0.2 drifts behind
    /// the page like a background, a negative share runs ahead of it. On the web it shifts with CSS <c>translate</c>, which
    /// leaves its own transform and animations alone, and never for a visitor who prefers reduced motion; on the desktop
    /// it shifts by its render transform, so a layer with one of its own should be wrapped. NaN, the default, for none.
    /// </summary>
    public static readonly AttachedProperty<double> ParallaxProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("Parallax", double.NaN);

    /// <summary>Gets <see cref="ParallaxProperty"/>.</summary>
    /// <param name="control">The layer.</param>
    /// <returns>The share of its distance it moves, or NaN.</returns>
    public static double GetParallax(Control control) => control.GetValue(ParallaxProperty);

    /// <summary>Sets <see cref="ParallaxProperty"/>.</summary>
    /// <param name="control">The layer.</param>
    /// <param name="value">The share of its distance it moves.</param>
    public static void SetParallax(Control control, double value) => control.SetValue(ParallaxProperty, value);

    /// <summary>Gets <see cref="RevealProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <returns>Whether its animations wait until it scrolls into view.</returns>
    public static bool GetReveal(Control control) => control.GetValue(RevealProperty);

    /// <summary>Sets <see cref="RevealProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <param name="value">Whether its animations wait until it scrolls into view.</param>
    public static void SetReveal(Control control, bool value) => control.SetValue(RevealProperty, value);

    /// <summary>Gets <see cref="IsWrittenProperty"/>.</summary>
    /// <param name="control">Any control.</param>
    /// <returns>Whether the control is written as HTML by Aioros's engine.</returns>
    public static bool GetIsWritten(Control control) => control.GetValue(IsWrittenProperty);

    /// <summary>Sets <see cref="IsWrittenProperty"/>.</summary>
    /// <param name="control">The engine's window.</param>
    /// <param name="value">Whether its tree is written.</param>
    public static void SetIsWritten(Control control, bool value) => control.SetValue(IsWrittenProperty, value);

    /// <summary>Gets <see cref="SwitchOffsetProperty"/>.</summary>
    /// <param name="control">The button.</param>
    /// <returns>The distance in pixels, or NaN.</returns>
    public static double GetSwitchOffset(Control control) => control.GetValue(SwitchOffsetProperty);

    /// <summary>Sets <see cref="SwitchOffsetProperty"/>.</summary>
    /// <param name="control">The button.</param>
    /// <param name="value">The distance in pixels.</param>
    public static void SetSwitchOffset(Control control, double value) => control.SetValue(SwitchOffsetProperty, value);
}
