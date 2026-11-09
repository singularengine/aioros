using Avalonia;
using Avalonia.Controls;

namespace Aioros.Markup;


/// <summary>The hints of what an element is and how it reads: its current mark, its line breaking, its scroll bars and fades and the slots a code input shows.</summary>
public sealed partial class Web
{
    /// <summary>
    /// The element is the current one of its set on the web (WAI-ARIA's <c>aria-current</c>: <c>page</c>, <c>step</c>,
    /// <c>location</c>, <c>date</c>, <c>time</c> or <c>true</c>), as a breadcrumb's page or a pagination's current link; any
    /// other value writes nothing. Ignored on the native targets.
    /// </summary>
    public static readonly AttachedProperty<string?> CurrentProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("Current");

    /// <summary>
    /// How the text's lines are broken on the web (CSS Text 4's <c>text-wrap</c>: <c>balance</c> evens the lines' widths,
    /// <c>pretty</c> avoids a short last line), inherited by the texts inside as CSS's is; any other value writes nothing. Ignored on the native targets.
    /// </summary>
    public static readonly AttachedProperty<string?> TextWrapProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("TextWrap", inherits: true);

    /// <summary>
    /// How long, in milliseconds, a scroll viewer whose bars its theme hides at rest keeps them shown on the web after the
    /// pointer leaves it (Radix ScrollArea's <c>scrollHideDelay</c>); the library hides them after the same delay on the
    /// native targets itself.
    /// </summary>
    public static readonly AttachedProperty<double> ScrollbarHideDelayProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, double>("ScrollbarHideDelay");

    /// <summary>
    /// The element's text shimmers on the web, as shadcn/ui's <c>shimmer</c> utility: a highlight sweeping across the
    /// text's color every two seconds (a gradient clipped to the text), reversed in a right-to-left element and still for
    /// a visitor who asks for less motion; inherited by the texts inside, as the utility clips to every text of its
    /// element. Ignored on the native targets, where the library animates the text itself.
    /// </summary>
    public static readonly AttachedProperty<bool> ShimmerProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, bool>("Shimmer", inherits: true);

    /// <summary>
    /// The scroll viewer's content fades out at its scrolled edges on the web, as shadcn/ui's <c>scroll-fade</c>
    /// utilities: <c>y</c> at the top and the bottom, <c>x</c> at the start and the end, or one edge (<c>t</c>, <c>b</c>,
    /// <c>s</c>, <c>e</c>), each fade growing to <c>min(12%, 40px)</c> over the first and last 96 px of the scroll; any other
    /// value writes nothing. Ignored on the native targets, where the library masks the viewer itself.
    /// </summary>
    public static readonly AttachedProperty<string?> ScrollFadeProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("ScrollFade");

    /// <summary>Gets <see cref="ScrollFadeProperty"/>.</summary>
    /// <param name="control">The scroll viewer.</param>
    /// <returns>The edges it fades at on the web, or null.</returns>
    public static string? GetScrollFade(Control control) => control.GetValue(ScrollFadeProperty);

    /// <summary>Sets <see cref="ScrollFadeProperty"/>.</summary>
    /// <param name="control">The scroll viewer.</param>
    /// <param name="value">The edges it fades at on the web.</param>
    public static void SetScrollFade(Control control, string? value) => control.SetValue(ScrollFadeProperty, value);

    /// <summary>
    /// The control holds a set of answers (a questionnaire's choices and inputs): a button inside it marked with
    /// <see cref="NeedsAnswerProperty"/> is enabled in the browser while it shows one. Ignored on the native targets.
    /// </summary>
    public static readonly AttachedProperty<bool> AnswersProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, bool>("Answers");

    /// <summary>
    /// The button waits for an answer on the web: enabled with no request while the region around it marked with
    /// <see cref="AnswersProperty"/> shows a checked choice or a filled text input, disabled otherwise (shadcn/ui's Next
    /// disabled until its item is answered). Ignored on the native targets, where the library enables it itself.
    /// </summary>
    public static readonly AttachedProperty<bool> NeedsAnswerProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, bool>("NeedsAnswer");

    /// <summary>Gets <see cref="AnswersProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <returns>Whether it holds a set of answers.</returns>
    public static bool GetAnswers(Control control) => control.GetValue(AnswersProperty);

    /// <summary>Sets <see cref="AnswersProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <param name="value">Whether it holds a set of answers.</param>
    public static void SetAnswers(Control control, bool value) => control.SetValue(AnswersProperty, value);

    /// <summary>Gets <see cref="NeedsAnswerProperty"/>.</summary>
    /// <param name="control">The button.</param>
    /// <returns>Whether it waits for an answer on the web.</returns>
    public static bool GetNeedsAnswer(Control control) => control.GetValue(NeedsAnswerProperty);

    /// <summary>Sets <see cref="NeedsAnswerProperty"/>.</summary>
    /// <param name="control">The button.</param>
    /// <param name="value">Whether it waits for an answer on the web.</param>
    public static void SetNeedsAnswer(Control control, bool value) => control.SetValue(NeedsAnswerProperty, value);

    /// <summary>Gets <see cref="ShimmerProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <returns>Whether its text shimmers on the web.</returns>
    public static bool GetShimmer(Control control) => control.GetValue(ShimmerProperty);

    /// <summary>Sets <see cref="ShimmerProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <param name="value">Whether its text shimmers on the web.</param>
    public static void SetShimmer(Control control, bool value) => control.SetValue(ShimmerProperty, value);

    /// <summary>Gets <see cref="CurrentProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The kind of current element, or null.</returns>
    public static string? GetCurrent(Control control) => control.GetValue(CurrentProperty);

    /// <summary>Sets <see cref="CurrentProperty"/>.</summary>
    /// <param name="control">The control.</param>
    /// <param name="value">The kind of current element.</param>
    public static void SetCurrent(Control control, string? value) => control.SetValue(CurrentProperty, value);

    /// <summary>Gets <see cref="TextWrapProperty"/>.</summary>
    /// <param name="control">The text.</param>
    /// <returns>How its lines are broken on the web, or null.</returns>
    public static string? GetTextWrap(Control control) => control.GetValue(TextWrapProperty);

    /// <summary>Sets <see cref="TextWrapProperty"/>.</summary>
    /// <param name="control">The text.</param>
    /// <param name="value">How its lines are broken on the web.</param>
    public static void SetTextWrap(Control control, string? value) => control.SetValue(TextWrapProperty, value);

    /// <summary>Gets <see cref="ScrollbarHideDelayProperty"/>.</summary>
    /// <param name="control">The scroll viewer.</param>
    /// <returns>The delay in milliseconds.</returns>
    public static double GetScrollbarHideDelay(Control control) => control.GetValue(ScrollbarHideDelayProperty);

    /// <summary>Sets <see cref="ScrollbarHideDelayProperty"/>.</summary>
    /// <param name="control">The scroll viewer.</param>
    /// <param name="value">The delay in milliseconds.</param>
    public static void SetScrollbarHideDelay(Control control, double value) => control.SetValue(ScrollbarHideDelayProperty, value);

    /// <summary>
    /// The text box whose characters this list's items show, one each, on the web: as the visitor types, the framework's
    /// script writes each character in its item and marks the item at the caret while the box has the focus (a one-time
    /// code's slots); the library shows them itself on the native targets.
    /// </summary>
    public static readonly AttachedProperty<Control?> SlotsProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, Control?>("Slots");

    /// <summary>
    /// The class this element of a <see cref="SlotsProperty"/> list's item takes while its item is the one at the caret;
    /// its look with that class is written for the web under the mark the framework's script sets.
    /// </summary>
    public static readonly AttachedProperty<string?> SlotActiveProperty =
        AvaloniaProperty.RegisterAttached<Web, Control, string?>("SlotActive");

    /// <summary>Gets <see cref="SlotsProperty"/>.</summary>
    /// <param name="control">The list.</param>
    /// <returns>The text box its items show, or null.</returns>
    public static Control? GetSlots(Control control) => control.GetValue(SlotsProperty);

    /// <summary>Sets <see cref="SlotsProperty"/>.</summary>
    /// <param name="control">The list.</param>
    /// <param name="value">The text box its items show.</param>
    public static void SetSlots(Control control, Control? value) => control.SetValue(SlotsProperty, value);

    /// <summary>Gets <see cref="SlotActiveProperty"/>.</summary>
    /// <param name="control">The element.</param>
    /// <returns>The class it takes while its item is at the caret, or null.</returns>
    public static string? GetSlotActive(Control control) => control.GetValue(SlotActiveProperty);

    /// <summary>Sets <see cref="SlotActiveProperty"/>.</summary>
    /// <param name="control">The element.</param>
    /// <param name="value">The class it takes while its item is at the caret.</param>
    public static void SetSlotActive(Control control, string? value) => control.SetValue(SlotActiveProperty, value);
}
