# Motion

Avalonia animations, transitions and pointer states written as CSS (`src/Adapters/Aioros.Avalonia/Html/Looks/MotionStyles.cs`, `StateStyles.cs`), and the transitions a page is in the middle of when it is written (`Html/Looks/Entrances.cs`). The browser runs them; no script is involved.

36 criteria. Each has an identifier and exactly one test.

## Motion

### MOTION-01: Style animations become CSS animations

- **Given** a style with an animation of opacity, size or background on a control (selector by type and classes)
- **When** the page is rendered
- **Then** the control has a CSS animation with the same duration, easing, delay, iteration count, direction and fill mode, and its key frames set the same properties at the same cues.

### MOTION-02: Transform animations become CSS transforms

- **Given** an animation whose key frames set properties of the control's render transforms (`ScaleTransform.ScaleY`, `TranslateTransform.X`, `RotateTransform.Angle`, `SkewTransform.AngleX`, alone or in a `TransformGroup`)
- **When** the page is rendered
- **Then** each CSS key frame sets `transform` to every transform of the control in order (`translate` in px, `scale`, `rotate` and `skew` in deg), taking the values the frame does not set from the control, and the control's `RenderTransformOrigin` becomes its `transform-origin`.

### MOTION-03: Transitions become CSS transitions

- **Given** a control with transitions of opacity, background, foreground, border brush and render transform
- **When** the page is rendered
- **Then** its CSS `transition` lists `opacity`, `background-color`, `color`, `border-color` and `transform` with each duration, easing (linear when none is set, as in Avalonia) and delay.

### MOTION-04: Pointer styles become hover and active rules

- **Given** styles for `:pointerover` and `:pressed` on a container with classes, and for its descendants (`Border.tile:pointerover TextBlock`)
- **When** the page is rendered
- **Then** the container's `:hover` and `:active` rules set the background, border brush, box shadow, opacity and transform those styles give it, and its descendants' rules under the container's `:hover` set their foreground, background, opacity and transform, with the values the styles set even when transitions animate towards them; properties the styles do not change, and properties set locally on the element (which win over styles in Avalonia), are not written.

### MOTION-07: Focus styles of containers become focus rules

- **Given** styles for `:focus-within` (border brush `#3366FF`) and `:focus-visible` (a box shadow) on a container with classes
- **When** the page is rendered
- **Then** the container has a `:focus-within` rule setting `border-color:#3366ff` and a `:focus-visible` rule setting the box shadow, as Avalonia gives the container while a descendant is focused or it is focused from the keyboard.

### MOTION-20: Animations follow the selectors Avalonia matches

- **Given** animations in styles whose selectors are not a bare type and classes, as a landing page staggers its entrances: `:is(Control).e1` (any control with the class) and a descendant selector `Border.card TextBlock`
- **When** the page is rendered
- **Then** each control Avalonia matches gets the animation (the selector matched as Avalonia matches it, not by its text) and a control it does not match (without the class, outside the card) gets none.

### MOTION-21: Style animations run at once under reduced motion

- **Given** a style animation written as CSS (MOTION-01)
- **When** the visitor prefers reduced motion (`prefers-reduced-motion: reduce`)
- **Then** it runs at once, with no duration and no delay, once, so the control shows the animation's end without moving (an entrance shows its final state, a loop stops); nothing else of the page changes.

*Precedent: WCAG 2.3.3 (animation from interactions) and the common `prefers-reduced-motion` reset that collapses durations instead of removing animations whose fill holds the visible state.*

### MOTION-22: Revealed controls are marked for their script

- **Given** a control with `a:Web.Reveal="True"` whose style animates it
- **When** the page is written
- **Then** its element carries `data-aioros-reveal` beside its animation, and a control without the hint carries no mark.

### MOTION-25: Gradient foregrounds paint the glyphs

- **Given** a text whose `Foreground` is a linear, radial or conic gradient (a heading in a brand gradient)
- **When** the page is written
- **Then** its element paints the gradient through its glyphs: `background` is the gradient, clipped to the text (`background-clip:text` and `-webkit-background-clip:text`) and the text's own color transparent (`color` and `-webkit-text-fill-color`), as Avalonia fills the glyphs with the brush; a solid foreground is written as its `color`, as before.

### MOTION-26: Swept gradients move across the text

- **Given** a text with a gradient foreground and `a:Web.Sweep="4"`
- **When** the page is written
- **Then** its gradient is drawn twice as wide as the text and repeats (`background-size:200% 100%`), and it moves one whole gradient to the right every 4 seconds without end (`@keyframes` from `background-position:0 0` to `-200% 0`, `linear`, `infinite`), still for a visitor who prefers reduced motion (MOTION-21); the hint on a text with a solid foreground writes nothing.

*Precedent: the animated gradient headings of Vercel's and Astro's landing pages, `background-clip:text` with a moving `background-position`.*

### MOTION-27: Counting texts are marked with their duration

- **Given** a text with `a:Web.CountUp="1.5"`
- **When** the page is written
- **Then** its element carries `data-aioros-count="1500"` (the count's milliseconds) and holds its text as it is, the value a visitor without script reads; a text without the hint carries no mark.

### MOTION-29: Position selectors stagger a panels children

- **Given** a panel whose styles stagger its children's entrance with Avalonia's position selectors (`StackPanel.cards > :is(Control):nth-child(2)` with a 100 ms delay, `:nth-child(3)` with 200 ms, `:nth-child(2n+1)` and `:nth-last-child(1)` for other looks)
- **When** the page is written
- **Then** each child gets the animation its position matches, with that delay, as Avalonia applies those styles, and a child no position style matches gets none.

*Precedent: CSS `:nth-child` staggering and Framer Motion's `staggerChildren`, here with the selectors Avalonia already has.*

### MOTION-30: Marquees move left by half their width without end

- **Given** a horizontal panel holding its items twice with `a:Web.Marquee="30"` (a band of logos)
- **When** the page is written
- **Then** its element moves left by half its width every 30 seconds, without end and evenly (`@keyframes` from `translateX(0)` to `translateX(-50%)`, `linear`, `infinite`), so the second copy takes the first's place seamlessly; it carries `data-aioros-marquee`, which pauses it while the pointer is over it, and it stands still for a visitor who prefers reduced motion.

*Precedent: the logo marquees of Vercel's and Linear's landing pages (a duplicated track translated by -50%).*

### MOTION-31: Spotlight cards carry their glow

- **Given** a card with `a:Web.Spotlight="#33FFFFFF"`
- **When** the page is written
- **Then** its element carries `data-aioros-spotlight` and its class `--aioros-spotlight:rgba(255,255,255,0.2)`, and the page's rules draw over it, while the pointer is over it, a glow of that color centred on `--x`, `--y` (`::after`, a `radial-gradient` fading to transparent, clipped to its corners, not taking the pointer); a card without the hint carries neither.

*Precedent: the pointer spotlights of Vercel's and Linear's cards.*

### MOTION-33: Parallax layers are marked with their factor

- **Given** a layer with `a:Web.Parallax="0.2"`
- **When** the page is written
- **Then** its element carries `data-aioros-parallax="0.2"`; a layer without the hint carries no mark.

*Precedent: the parallax layers of Apple's and Astro's landing pages.*

### MOTION-35: Size key frames are written in pixels

- **Given** a style animation whose key frames set `Width` and `Height` (a bar that grows, a line that types)
- **When** the page is written
- **Then** each key frame writes them in pixels (`height:16px`, `width:345px`), so the browser plays them.

## Button theme

### MOTION-05: Template part transitions become CSS transitions

- **Given** a `Button` whose control theme gives its content presenter transitions (a `BrushTransition` of `Background` over 150 ms with the spline easing `0.4,0,0.2,1`, a `BoxShadowsTransition` of `BoxShadow` over 200 ms after a 50 ms delay) and a `^:pointerover` style that changes the background
- **When** the page is rendered
- **Then** the `:hover` rule carries the pointed-at background, not the rest one, and the `<button>`'s CSS `transition` lists `background-color 150ms cubic-bezier(0.4,0,0.2,1) 0ms` and `box-shadow 200ms linear 50ms` with the button's own `transform` transition.

### MOTION-06: Named easings become CSS curves

- **Given** transitions with Avalonia's named easings
- **When** the page is rendered
- **Then** each is written as its CSS curve: the polynomial, sine, exponential, circular and back easings as their `cubic-bezier(…)` (`QuarticEaseOut` as `cubic-bezier(.25,1,.5,1)`, `BackEaseIn` as `cubic-bezier(.36,0,.66,-.56)`), and the elastic and bounce easings as a `linear(…)` function sampled from the easing itself, starting at `0` and ending at `1`.

## Overlay theme

### MOTION-08: A page is written as its transitions end

- **Given** a control with transitions of opacity and of its render transform whose values a command changed just before the page is written (a surface entering from a `starting` class at opacity 0 and scale .95), and a control whose theme runs an opacity animation from 0 (a notification card's entrance)
- **When** the page is written
- **Then** each is written at the value its transition or animation ends at (opacity 1, no transform), and the transitioned control's class has a `@starting-style` rule with the look it entered from (`opacity:0;transform:matrix(.95,0,0,.95,0,0)`), so the browser plays its entrance; a control without a running transition has none.

### MOTION-09: Overlays enter and leave as their theme transitions

- **Given** a `Flyout` whose presenter's theme gives its root border an opacity transition and an entrance from opacity 0 as Avalonia shows it
- **When** it is rendered
- **Then** the popover's look carries the transition with `display` and `overlay` as `allow-discrete` transitions of the same length, a `@starting-style` rule with opacity 0, and the same look while it is closed (`:not(:popover-open)`), so it fades in when it opens and out when it closes.

### MOTION-10: Entrances are written however the clock moves

- **Given** a page whose first control enters with a 1 ms opacity transition from 0 as it is shown, and whose next control moves the platform's animation clock ten seconds on while the page is realized (as real time moves it during a long page's realization)
- **When** the page is written
- **Then** the first control is written at rest with its entrance (`@starting-style` with opacity 0): the engine realizes and writes a page, and answers a post, on a still clock of its own, so an entrance started then (a page control's, a flyout's probed presenter's) is written however long the writing takes.

### MOTION-11: Entrances stand still for less motion

- **Given** a page control and a flyout's presenter that enter by their transitions (MOTION-08, MOTION-09)
- **When** the page is written
- **Then** each entrance's class also has, under `@media (prefers-reduced-motion:reduce)`, `transition:none!important`, so for a visitor who asks for less motion the control shows, and the popover opens and closes, at once and at rest, while their `@starting-style` and closed look stay for everyone else.

*Precedent: Tailwind's `motion-safe:` and `motion-reduce:` variants (tw-animate-css's `animate-in` under `motion-safe`), WCAG 2.3.3.*

### MOTION-12: Overlays scale from their themes origin

- **Given** a `Flyout` whose presenter's root border enters by its transitions from its top center (`RenderTransformOrigin="50%,0%"`, as a popover opening below its trigger scales from the trigger's side, Radix's `--radix-popover-content-transform-origin`)
- **When** it is rendered
- **Then** its entrance's class gives the popover `transform-origin:50% 0%`; a surface entering from its center writes no origin.

### MOTION-14: Menu bars and sub menus enter as their popups surfaces

- **Given** a menu bar whose items' template popups show their menus on a surface that enters by its transitions (opacity from 0 over 150 ms, its origin at its top start), a sub-menu's item having such a popup too
- **When** the page is written
- **Then** the bar's menu and the sub-menu each carry their surface's entrance (`@starting-style` with opacity 0, the same look while closed, `transform-origin:0% 0%`), as a menu flyout's presenter does (MOTION-09).

### MOTION-15: Overlays leave as their theme closes them

- **Given** a `Flyout` whose presenter's root enters from opacity 0 and 8 px up over 200 ms, and whose theme gives that root, while the presenter is `:closing`, opacity 0, 95% and transitions of 100 ms (tw-animate-css's `animate-out fade-out-0 zoom-out-95`, which leaves without the slide and may be shorter, as Radix's `data-state="closed"`)
- **When** it is rendered
- **Then** the popover's `@starting-style` keeps the entrance with its slide, and its look while closed is `opacity:0;transform:matrix(0.95,0,0,0.95,0,0)` with its own `transition` (opacity and transform over 100 ms, `display` and `overlay` 100 ms `allow-discrete`), so it leaves as its theme closes it; a theme with no `:closing` look leaves as it came (MOTION-09).

### MOTION-19: A tip enters and leaves as its theme moves it

- **Given** a button with a text tip whose `ToolTip` theme's template root enters from opacity 0 and 8 px up by transitions over 150 ms with `ease` from its bottom center (Aioros.Cosmo's tooltip: tw-animate-css's `fade-in-0 zoom-in-95 slide-in-from-bottom-2`), and which leaves at opacity 0 and 95% while the tip is `:closing` (`fade-out-0 zoom-out-95`)
- **When** the page is written
- **Then** the tip's bubble carries a class whose resting rule is the closing look with the root's transitions and its origin (`transform-origin:50% 100%`, `visibility` hidden after the exit), whose shown rule (`.tip:hover>`, `.tip:has(:focus-visible)>`) is the resting look with an animation from the entrance look (`@keyframes` from `opacity:0;transform:matrix(1,0,0,1,0,-8)`) over 150 ms with `ease` after the tip's delay, and which plays neither for a visitor who asks for less motion (`prefers-reduced-motion:reduce`) or after Escape (`data-off`); a tip whose theme moves nothing keeps Aioros's fade.

*Precedent: Radix Tooltip's `data-state` with tw-animate-css's `animate-in` and `animate-out`, CSS animations with a `backwards` fill over a delay.*

## Dialog post

### MOTION-13: A prompts mask fades in as its theme does

- **Given** a dialog whose mask fades in from opacity 0 over 150 ms as it is shown (Radix's overlay's `fade-in-0`)
- **When** a post opens it
- **Then** the prompt's mask (`pm`) carries the mask's `transition:opacity 150ms` and the mask's entrance class (`@starting-style` with opacity 0), so it fades in under the surface; a mask that does not enter has neither.

### MOTION-17: Notices rise and leave as their cards theme

- **Given** a toaster whose cards' root enters from opacity 0 and 8 px up by its transitions and, while the card is `:closing`, leaves at opacity 0 and 20 px down over 400 ms (sonner's rise and fall)
- **When** a post shows a toast
- **Then** the card's root carries its entrance (`@starting-style`), and its notice leaves after its expiration with the card's closing look as a 400 ms animation of its own (`opacity:0;transform:matrix(1,0,0,1,0,20)`) in place of the framework's 300 ms fade, at once for a visitor who asks for less motion; a card with no closing look keeps the framework's fade.

## Select theme

### MOTION-16: Customizable select lists enter and leave as the themes popup

- **Given** a `ComboBox` whose theme's popup border enters by its transitions (opacity from 0 and 4 px up over 150 ms) and leaves at opacity 0 while it is `:closing`
- **When** the page is written
- **Then** its customizable select's picker (`::picker(select)`) carries the transitions with `display` and `overlay` as `allow-discrete`, the closing look while closed, its resting look while the select is `:open`, the entrance as the open picker's `@starting-style`, and no transition for a visitor who asks for less motion, so the list enters and leaves as the theme's popup does.

## Modal

### MOTION-18: Marked dialogs enter and leave as their parts

- **Given** a library dialog opened in the browser (`a:Web.Modal`, OVERLAY-10) whose mask fades in over 150 ms and whose surface fades in over 200 ms, each leaving at opacity 0 while `:closing` (the surface over 100 ms, the mask over 150 ms)
- **When** the page is written
- **Then** in its closed `<dialog>` the mask and the surface carry their transitions and their entrances (`@starting-style` with opacity 0), each has its closing look and exit transition while the dialog is not `open` (`#dlg1:not([open]) .en…`), and the dialog keeps its `display` and top layer for the longest exit (`transition:display 150ms allow-discrete,overlay 150ms allow-discrete`), none of it for a visitor who asks for less motion, so it enters and leaves as Radix's dialog.

## Reveal script

### MOTION-23: Revealed elements play when they first scroll into view

- **Given** a page whose elements marked `data-aioros-reveal` animate (MOTION-01), one in the viewport and one below it
- **When** the page opens
- **Then** the one in view plays at once and the reveal module (loaded only by pages with the mark) holds the one below at its animation's start, paused with its first key frame showing (opacity 0); when it is scrolled into view it plays from its start, and scrolling away and back does not play it again (once, Framer Motion's `whileInView` with `once`), with no request; with no script the mark changes nothing and every animation plays as the page opens.

*Precedent: Framer Motion `whileInView` and `viewport.once`, AOS; CSS scroll-driven animations (`animation-timeline: view`) tie progress to the scroll and play backwards, so they were not used.*

### MOTION-24: Revealed elements stay revealed when the page is morphed

- **Given** an element revealed by the reveal module (MOTION-23) inside the page
- **When** the page is answered again and morphed in place (a post's answer, a live region the server pushes)
- **Then** it stays revealed: it keeps `data-aioros-revealed`, which the server's markup does not carry, and its animation is not paused or played again.

## Count script

### MOTION-28: Numbers count up in their own format when they come into view

- **Given** texts marked `data-aioros-count` holding numbers in their own formats (`85,620`, `10.2×`, `1.234,5 ms`), one in the viewport and one below it
- **When** the page opens
- **Then** the one in view counts from zero to its value over its duration, easing out, each step written in the text's own format (the same prefix and suffix, grouping and decimal separators, and number of decimals), ending on exactly its text; the one below shows zero in that format until it is scrolled into view, then counts the same way, once; a text with no number is left as it is, a text the page changes while it counts (a live update) keeps the page's text, and for a visitor who prefers reduced motion every text shows its value at once.

*Precedent: the counting figures of Vercel's and Stripe's landing pages (`react-countup`, Framer Motion's `animate` on a number).*

## Spotlight script

### MOTION-32: The glow follows the pointer

- **Given** a card marked `data-aioros-spotlight` in the browser
- **When** the pointer moves over it
- **Then** the framework's script sets `--x` and `--y` on it to the pointer's place inside it, in pixels, so its glow follows the pointer, with no request; the pointer over another element sets nothing.

## Parallax script

### MOTION-34: Layers shift against the scroll by their share

- **Given** an element marked `data-aioros-parallax="0.5"` inside a scrolled container
- **When** its container scrolls
- **Then** the framework's script shifts it with CSS `translate` (which leaves its own `transform` and animations alone) by half its distance from the viewport's middle, against the scroll: the element whose middle is 200 px below the viewport's is shifted 100 px up, and once scrolled to the middle it is not shifted; for a visitor who prefers reduced motion it is never shifted.

### MOTION-36: Each frame reads every layer before writing any

- **Given** three layers marked `data-aioros-parallax` in a scrolled container
- **When** the container scrolls
- **Then** each frame of the parallax reads every layer's position before it writes any layer's shift, without clearing a shift to measure under it, so a frame costs one layout however many layers move (the shifts stay those of MOTION-34), and the layers are promoted for compositing (`will-change: translate`).

*Precedent: the "read, then write" rule against layout thrashing (FastDOM, the browsers' own guidance).*
