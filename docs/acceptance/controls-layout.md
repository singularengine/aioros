# Controls: layout

Panels, borders and wrappers (`src/Adapters/Aioros.Avalonia/Html/Families/LayoutFamily.cs` and the tree writer).

73 criteria. Each has an identifier and exactly one test.

## Layout controls

### LAYOUT-01: Stack panels are flex

- **Given** a `StackPanel`
- **When** it is rendered
- **Then** it is a flex container in its orientation with its spacing as the gap.

### LAYOUT-02: Grids are CSS grids

- **Given** a `Grid` with column and row definitions
- **When** it is rendered
- **Then** it is a CSS grid whose tracks follow the definitions (pixels, `auto`, stars as `fr`) and whose children are placed by row, column and span.

### LAYOUT-03: Wrap panels wrap and uniform grids share columns

- **Given** a `WrapPanel` and a `UniformGrid`
- **When** they are rendered
- **Then** the first wraps its row with its item and line spacing, and the second is a grid of equal columns.

### LAYOUT-04: Borders become boxes

- **Given** a `Border` with background, border, corner radius, padding and shadow
- **When** it is rendered
- **Then** each becomes the matching CSS property.

### LAYOUT-05: Horizontal scroll viewers scroll

- **Given** a `ScrollViewer` that scrolls horizontally
- **When** it is rendered
- **Then** its element scrolls horizontally with `overflow-x:auto`.

### LAYOUT-06: Plain wrappers add no element

- **Given** wrappers with no look of their own (`ContentControl`, `UserControl`, `Decorator`)
- **When** they are rendered
- **Then** they add no element and their child takes their place.

### LAYOUT-07: Panels overlay their children

- **Given** a `Panel` with several children
- **When** it is rendered
- **Then** it is a one-cell CSS grid (`grid-template:minmax(0,1fr)/minmax(0,1fr)`) where every child sits in `grid-area:1/1`, overlaid, with its alignment as self-alignment.

### LAYOUT-08: Dock panels dock in order around the last child

- **Given** a `DockPanel` with children docked to the top, left, right and bottom, then a last child
- **When** it is rendered
- **Then** it is a CSS grid with an `auto` track per docked child around a `minmax(0,1fr)` center and its spacings as gaps, each docked child spans the area left when it docks (`grid-area`), and the last child fills the center; with `LastChildFill="False"` the last child docks too.

### LAYOUT-09: Relative panels position with anchors

- **Given** a `RelativePanel` whose children align with the panel and with named siblings
- **When** it is rendered
- **Then**
  - The panel is `position:relative` with `anchor-scope:all` and the height Avalonia measures as `min-height`, each child is absolutely positioned, a sibling something refers to is an anchor (`anchor-name:--Name`), and the attached properties become insets: `RightOf` is `left:anchor(--A right)`, `AlignTopWith` is `top:anchor(--A top)`, `Below` is `top:anchor(--A bottom)`, `AlignRightWithPanel` is `right:0`, `AlignBottomWithPanel` is `bottom:0`, and `AlignHorizontalCenterWithPanel` centers with `left:0;right:0;margin-inline:auto` (and `AlignVerticalCenterWithPanel` with `top:0;bottom:0;margin-block:auto`), with `fit-content` on an axis without its own size, and never with a translation, which CSS anchors ignore, so a sibling placed relative to a centered child (`Above` it, `AlignRightWith` it) lands where Avalonia puts it.
  - A sibling must be declared before the children that refer to it (CSS anchors precede their positioned elements).

### LAYOUT-10: Reversible stack panels reverse their flow

- **Given** a `ReversibleStackPanel` with `ReverseOrder="True"`
- **When** it is rendered
- **Then** it is a flex container whose direction is `column-reverse`, or `row-reverse` when it is horizontal, packed with `justify-content:flex-end` so the reversed items start where Avalonia starts them.

### LAYOUT-11: Grid splitters are focusable separators

- **Given** a Fluent `GridSplitter` between two star columns (`*,Auto,*`) and one with `ResizeDirection="Rows"` between two star rows
- **When** they are rendered
- **Then** each is a focusable `<div role="separator" tabindex="0">` in its grid cell with the theme background and minimum size, `touch-action:none` and the resize cursor, and no children (Fluent's template is a childless border): the first `aria-orientation="vertical"` with `aria-valuenow="50"`, `aria-valuemin="0"` and `aria-valuemax="100"` (the track before it as a percentage of the grid's star tracks, within the definitions' limits), `data-aioros-split="c 0 2"` (its axis and the tracks it resizes), `data-aioros-tracks="s a s"` (star, auto or fixed tracks) and `data-aioros-step="10px"` (its `KeyboardIncrement`), the second `aria-orientation="horizontal"` and `data-aioros-split="r 0 2"`; the split module resizes them (LAYOUT-62).

*Precedent: Avalonia's `GridSplitter` (the definitions on either side by its `ResizeBehavior`, the arrows by `KeyboardIncrement`).*

### LAYOUT-12: Render transforms become CSS transforms

- **Given** controls with a `RenderTransform` (a rotation, or a group of scale and translation) and a `RenderTransformOrigin`
- **When** they are rendered
- **Then** each element has `transform:matrix(a,b,c,d,e,f)` with the transform's matrix and `transform-origin` from the origin (in percent when relative, in pixels when absolute, omitted at the default center).

### LAYOUT-13: Layout transforms take the transformed size

- **Given** a `LayoutTransformControl` rotating its child by 90 degrees
- **When** it is rendered
- **Then** its box has the size of the transformed child as Avalonia measures it, and the child is centered in it and rotated once with `transform:matrix(0,1,-1,0,0,0)` (the render transform Avalonia puts on the child for the layout transform is not written again).

### LAYOUT-14: Separators are rules

- **Given** a `Separator`
- **When** it is rendered
- **Then** it is an `<hr>` with no border, the theme stroke as its background, its theme height and margin.

### LAYOUT-15: Group boxes are fieldsets

- **Given** a `GroupBox` with a header
- **When** it is rendered
- **Then** it is a `<fieldset>` with the theme border and corner radius and the theme padding around its content, whose `<legend>` holds the header in the theme font size, aligned where Avalonia draws it.

### LAYOUT-16: Headered content controls write their template

- **Given** a `HeaderedContentControl`
- **When** it is rendered
- **Then** its Fluent template is written: a box with the theme padding holding a grid whose first row is the header and whose second row is the content.

### LAYOUT-17: Theme variant scopes resolve their variant

- **Given** a dark `ThemeVariantScope` inside a light page
- **When** it is rendered
- **Then** its element sets `color-scheme:dark` and the scope's foreground, and the resources inside it resolve to the dark variant (a `Border` whose background is `SystemControlBackgroundAltHighBrush` is `#000000`).

### LAYOUT-18: Acrylic borders blur the backdrop

- **Given** an `ExperimentalAcrylicBorder` with a material
- **When** it is rendered
- **Then** its background is the material's tint layered over its luminosity color as Avalonia composes them, with `backdrop-filter:blur(30px)` and its corner radius.

### LAYOUT-19: Virtualizing panels write their items

- **Given** an `ItemsControl` whose items panel is a horizontal `VirtualizingStackPanel`
- **When** it is rendered
- **Then**
  - Every item is realized and written in a `row` flex stack.
  - A `Carousel`, whose panel is a `VirtualizingCarouselPanel`, writes all its items as scroll-snap slides (ITEMS-08).

### LAYOUT-20: Templated controls write their template

- **Given** a custom `TemplatedControl` with a `ControlTheme`
- **When** it is rendered
- **Then** the elements of its template are written with their template bindings resolved.

### LAYOUT-21: Gradient brushes become CSS gradients

- **Given** borders whose backgrounds are a `LinearGradientBrush`, a `RadialGradientBrush` and a `ConicGradientBrush`
- **When** they are rendered
- **Then** each background is the matching CSS gradient: `linear-gradient` at the angle from its start to its end point with its stops placed along that line, `radial-gradient(ellipse ...)` with its radii and center, and `conic-gradient(from ...)` with its angle and center.

### LAYOUT-22: Opacity clipping and stacking are CSS

- **Given** a control with `Opacity`, `ClipToBounds` and `ZIndex`
- **When** it is rendered
- **Then** its element has `opacity`, `overflow:hidden` and `z-index`.

### LAYOUT-23: Self drawn controls are SVG of their render

- **Given** a custom `Control` that draws itself by overriding `Render` with a solid fill
- **When** it is rendered
- **Then** it is an inline `<svg>` at its size (`width`, `height` and `viewBox` from its layout, `aria-hidden` without an automation name) whose `<path>` is the filled geometry with its color, recorded from its `Render` through a `DrawingGroup`'s drawing context instead of a PNG.

### LAYOUT-24: Sized stretched children are centered

- **Given** children with an explicit width or height and a stretch alignment in a vertical stack, a horizontal stack and a grid cell
- **When** they are rendered
- **Then** each is centered on that axis (`align-self:center`, or `justify-self:center` in the grid), as Avalonia centers a sized child in its slot.

### LAYOUT-25: Self drawn strokes transforms and text are SVG

- **Given** a self-drawn control with an automation name that strokes a line with a dashed, round-capped pen, pushes a transform and an opacity, and draws text
- **When** it is rendered
- **Then** its `<svg>` has `role="img"` and the name as `aria-label`, the line as a path with `fill="none"`, its stroke color, `stroke-width`, `stroke-dasharray` scaled by the thickness and `stroke-linecap="round"`, a `<g>` with the pushed `transform="matrix(...)"` and `opacity`, and the text as a filled path of its glyph outlines.

### LAYOUT-26: Unrecordable drawings stay images of their render

- **Given** a self-drawn control whose drawing SVG would not reproduce here (a conic gradient brush, which SVG has no paint for) and one that the recording context refuses (a custom draw operation)
- **When** they are rendered
- **Then** each is still an `<img>` whose source is Avalonia's own drawing as a PNG data URI at its size.

### LAYOUT-27: Border thickness keeps its width without a brush

- **Given** a `Border` with `BorderThickness="3"` and no `BorderBrush`, and a Fluent `Button` (whose theme gives a 1 px border with a transparent brush)
- **When** they are rendered
- **Then** each keeps its border width with a transparent color (`border-color:transparent;border-width:3px 3px 3px 3px` and `1px 1px 1px 1px`), as Avalonia lays a border's thickness out whatever its brush, so the `Button` is 30 px tall as in Fluent, never 2 px shorter.

### LAYOUT-37: Bitmaps and rounded clips stay images of their render

- **Given** a self-drawn control that draws a bitmap and one that pushes a rounded clip, which the recording drawing context refuses with `NotImplementedException` (Ursa's `ImageViewer` and `QRCode`)
- **When** they are rendered
- **Then** each is an `<img>` whose source is Avalonia's own drawing as a PNG data URI at its size, and the page renders instead of answering 500.

### LAYOUT-38: Greedy drawings are measured against the viewport

- **Given** a self-drawn control without a size whose measure asks for all the space it is offered (Ursa's `Clock`)
- **When** it is rendered
- **Then** it is measured against the page's viewport (its window's 1280 by 800), as Avalonia's top level offers its client size to its content, and it is drawn at that finite size instead of failing with "Invalid size returned for Measure".

### LAYOUT-48: Controls carry their ID name and responsive classes

- **Given** controls written by their own writer (a `CheckBox`, a `RadioButton`, a `ToggleSwitch`, a `ComboBox`, a `Slider`, a `ProgressBar`, a `DatePicker`, a `ColorPicker`) each with `a:Web.Id`, `AutomationProperties.Name` and `a:Web.HideBelow`
- **When** they are rendered
- **Then** each carries the id and the name as `aria-label` on the element that takes its input (the native `<input>`, `<select>` or `<progress>`) and the hide-below class on its outer element, as containers carry them, through one shared root writer.

### LAYOUT-49: Stacked grids reach the cells of wrappers without a box

- **Given** a `Grid` with `a:Web.StackBelow="600"` and two columns, whose cells are placed elements and wrappers that add no box of their own (a composable view's or a page region's `display:contents` element, nested one or two deep)
- **When** the viewport is narrower than 600 px
- **Then** every element that takes a cell, a wrapped one included, spans the single column and flows in rows (`grid-column:1/-1;grid-row:auto`), so wrapped views stack as plain children do without wrapping them in a `Panel`.

*Precedent: CSS Display Level 3 (`display:contents` makes the element's children the grid's items, so the rule must reach them); Tailwind's responsive utilities applied to the items themselves.*

### LAYOUT-50: CSS gradients are placed on the box like Avalonia

- **Given** borders 200 by 100 pixels whose backgrounds are a `LinearGradientBrush` from corner to corner, one with absolute points, one with `SpreadMethod="Reflect"`, one with `Repeat` whose stops do not reach the ends, and a `RadialGradientBrush` with `Reflect`
- **When** they are rendered
- **Then** each CSS gradient is placed on the box as Avalonia places it, in pixels: the angle of the line from the start to the end point on the box (`116.565deg` corner to corner, not `135deg`) with each stop at its projection on that line, absolute points as pixels on the box, a reflected spread as a repeating gradient with its stops mirrored over twice the length, a repeated one with its first and last colors held to the ends of the period.

### LAYOUT-51: Gradient backgrounds of panels and plain borders are kept

- **Given** a `Grid` with a gradient background and a `Border` whose only look is a gradient background
- **When** they are rendered
- **Then** each is an element with its CSS gradient as background: the panel's gradient is written as a border's is, and the border is not dropped as a wrapper.

### LAYOUT-52: Gradient border brushes and strokes are CSS gradients

- **Given** a `Border` with a background, a corner radius and a `LinearGradientBrush` border brush, one with a gradient border brush and no background, and a `Rectangle` without a size with a fill and a gradient stroke
- **When** they are rendered
- **Then** each border is transparent and shows the gradient: under a fill, as a background layer on the border box below the fill clipped to the padding box (`linear-gradient(#ffffff,#ffffff) padding-box,linear-gradient(...) border-box`, keeping the corner radii), and without one as `border-image` with a slice of 1.

### LAYOUT-53: Self drawn gradients are SVG gradients

- **Given** a self-drawn control that fills a rectangle with a `LinearGradientBrush` and strokes an ellipse with a `RadialGradientBrush` pen
- **When** it is rendered
- **Then** it is an inline `<svg>` (not a PNG) whose `<defs>` hold the two gradients in user space on Avalonia's targets (the rectangle's bounds; the ellipse's bounds inflated by half the pen's thickness), referenced by the path's `fill` and the stroked path's `stroke`.

### LAYOUT-54: Window filling scroll viewers make a viewport shell

- **Given** a window-filling shell, a `DockPanel` with a header docked at the top, a navigation `ScrollViewer` docked at the left and a content `ScrollViewer` filling the rest
- **When** the page is rendered
- **Then** the document becomes a viewport shell as Avalonia lays the window out: the form takes the viewport's height (`height:100dvh`, `overflow:hidden`), every element from the shell to each `ScrollViewer` fills its parent (`vf`, `min-height:0`, `flex:1 1 0` in a column) and each `ScrollViewer` is a scroll container (`vs`: `overflow:hidden auto`, its content as wide as the viewer as Avalonia's disabled horizontal scroll bar gives it; `vsx`, scrolling both ways, for a viewer whose horizontal scroll bar is `Auto` or `Visible`), so the header and the navigation stay while the content scrolls; a `ScrollViewer` inside a vertical `StackPanel`, which Avalonia measures with an infinite height, makes no shell; and a client navigation scrolls back to the top the shell's scroll container that holds the page.

### LAYOUT-55: Items in a grid panel take their cells

- **Given** an `ItemsControl` whose `ItemsPanel` is a `Grid` with three columns (`Auto,*,Auto`) and whose items sit in `Grid.Column` 0, 1 and 2 (Aioros.Cosmo's input group and its addons)
- **When** the page is rendered
- **Then** each item is placed in its own column (`grid-column:1`, `2` and `3`), read from the panel that holds it (its visual parent), as Avalonia's grid places it, instead of every item in the first cell.

### LAYOUT-56: Content presenters write their own box

- **Given** a `ContentControl` with `Padding="12,6,0,6"`, whose template gives its content presenter that padding (as a library control written through its template, Aioros.Cosmo's input group addon, does), and one without
- **When** the page is rendered
- **Then** the padded one's presenter is a box with `padding:6px 0px 6px 12px` around its content, as Avalonia lays it out, instead of a wrapper the web drops; the plain one's content is written alone.

### LAYOUT-57: Rounded template parts clip their content

- **Given** a templated control whose template's `Border` part has `ClipToBounds="True"` and the control's `CornerRadius` of 16 and holds an `Image` (Aioros.Cosmo's `c:Avatar`), and a templated control whose part does not clip
- **When** the page is rendered
- **Then** the clipping part's element has `overflow:hidden` with `border-radius:16px 16px 16px 16px`, so its content is cut at the rounded corners as Avalonia clips a border's children to its rounded bounds, and the other part has no `overflow:hidden`.

### LAYOUT-58: Stretched controls keep their margins within their max width

- **Given** a `Border` stretched horizontally with `MaxWidth="512"` and `Margin="16"` (Aioros.Cosmo's `c:DialogSurface`) and one with `MaxWidth="200"` and `Margin="8,0,24,0"`
- **When** the page is rendered
- **Then** the first is `margin:16px auto 16px auto;width:calc(100% - 32px)` and the second `margin:0px auto 0px max(8px,calc((100% - 216px) / 2))` with `width:calc(100% - 32px)`, as Avalonia's layout gives a stretched control the slot less its margins, no wider than its `MaxWidth`, centered in that slot, so a 390 px viewport keeps 16 px on each side of the first.

### LAYOUT-60: A themed scroll viewer scrolls inside its frame

- **Given** a `ScrollViewer` 384 px wide whose own control theme (not Fluent's) frames its `ScrollContentPresenter` in a `Border` with the viewer's 1 px `#e5e5e5` border and 6 px radius and thin bars whose thumb is `#e5e5e5` (Aioros.Cosmo's scroll area, Radix's), scrolling horizontally (`Auto`) and not vertically (`Disabled`), holding a horizontal stack of two 300 px boxes, and a Fluent `ScrollViewer`
- **When** the page is rendered
- **Then** the themed viewer is one element with the frame's border and radius that scrolls (`overflow-x:auto;overflow-y:hidden`) with `scrollbar-width:thin` and `scrollbar-color:#e5e5e5 transparent`, its content written directly in it at its own width (`min-width:max-content`, so the boxes keep 300 px as the scroll content presenter measures them), and the Fluent viewer is written as before.

### LAYOUT-59: Themed grid splitters write their template and limits

- **Given** a `GridSplitter` whose template is more than a border (a panel holding a line and a 12 by 16 grip box), with `a:Web.ResizeStep="5"` and `a:Web.CollapsedSize="0"`, between the columns `25*,Auto,75*` whose first has `a:Web.MinSize="10"` and `MinWidth="40"` and last `a:Web.MinSize="25"`
- **When** it is rendered
- **Then** the separator holds its template's parts (the grip box), carries `data-aioros-step="5%"`, `data-aioros-collapse="0"` and `data-aioros-px="40 Infinity 0 Infinity"` (the two definitions' limits in pixels, which the split module adds to the percentages), and `aria-valuenow="25"`, `aria-valuemin="10"` and `aria-valuemax="75"` (the first column's share of the star tracks, its minimum, and all but the last's minimum).

*Precedent: react-resizable-panels 4.5.8 (`keyboardResizeBy` as a percentage, `collapsedSize`, `minSize`, `maxSize`), whose sizes are percentages of the group.*

### LAYOUT-66: A stretched child of a fixed size is centered in its border

- **Given** a 32 px `Border` holding a 16 px `Viewbox` with its default alignments (stretched, of a fixed size, as the toggle of Aioros.Cosmo's collapsible holds its icon), and the same border holding a 16 px box with a 4 px margin
- **When** the page is rendered
- **Then** the viewbox is centered across and down its border (`align-self:center`, `margin-top:auto;margin-bottom:auto`), as Avalonia centers a stretched control of a fixed size in its slot, where it sat at the top; the box with a margin keeps its margin.

### LAYOUT-67: A stretched only child takes its parents height

- **Given** a `Border` 120 px high holding, stretched and without a height, a `Border` with a background around a `ScrollViewer` over a 400 px column (Aioros.Cosmo's message scroller filling the card under its header)
- **When** the page is rendered
- **Then** the stretched only child is written `flex:1 1 0%;min-height:0` (Tailwind's `flex-1 min-h-0`): in a parent of a definite height it takes that height without growing past it, as Avalonia arranges a stretched child in its slot, so the viewer scrolls, and in a parent with no height the percentage basis is its content, as Avalonia measures it; the scroll viewer's own content, which Avalonia measures with no height, keeps `flex:1 0 auto`.

### LAYOUT-68: Viewers follow their end and buttons scroll there

- **Given** a `ScrollViewer` with `a:Web.FollowsEnd="True"` 100 px high over a 400 px column, and a button with `a:Web.ScrollsToEnd` naming it in their name scope (Aioros.Cosmo's `c:MessageScroller` and its scroll-to-end button)
- **When** the page is rendered
- **Then** the viewer is a scroll container that starts at its end (`flex-direction:column-reverse`, a spacer before its content keeping a content shorter than the viewer at its top, `data-aioros-follow`), and the button carries `data-aioros-scrolls` with the viewer's name, which the viewer carries as `data-aioros-scroll-name`.

*Precedent: `@shadcn/react`'s `MessageScroller`, CSS's `column-reverse` scroll origin.*

### LAYOUT-69: A scroll viewers own template keeps its contents height

- **Given** a `ScrollViewer` 200 px square whose own template is a `Grid` holding its `ScrollContentPresenter` and its bars (Semi.Avalonia's, not framed in a border) over a `UniformGrid` 300 px square
- **When** the page is rendered
- **Then** the template's parts written inside the viewer's element keep their content's height (`flex:1 0 auto`, not the `flex:1 1 0%;min-height:0` of LAYOUT-67), since the viewer's element is what scrolls on the web, so the grid keeps its 300 px rows of 150 px as Avalonia lays them out.

### LAYOUT-86: A grid splitters position travels and is applied as a drag

- **Given** a grid of 25* and 75* columns around a `GridSplitter` whose `DragCompleted` reads the first column's laid-out width, one splitter with `a:Web.Delay` 150 and one without
- **When** the page is written and when a post carries the first splitter's field as 40
- **Then** each separator holds a hidden field with its value (`<input type="hidden" name="f0" value="25">`), the delayed one marked `data-change data-delay="150"`; and the post resizes the two columns as Avalonia's `GridSplitter` ends a drag (40* and 60*, the grid laid out again) and raises its `DragCompleted`, which reads the first column 40% of the width its stars share (158 of 395 px beside Fluent's 6 px splitter); a field that names the value already shown changes nothing and raises nothing.

*Precedent: Avalonia's `GridSplitter` (the definitions resized in star units, `DragCompleted` at the end of a drag), react-resizable-panels' `onLayout` after a drag, the "selections that post on change" decision.*

### LAYOUT-88: A scroll viewers max height caps its box

- **Given** a Fluent scroll viewer and a scroll viewer whose own theme frames its content in a border, each with `MaxHeight` 120 over a panel 300 px tall, and a third without `MaxHeight`
- **When** the page is written
- **Then** each of the first two scrolling boxes carries `max-height:120px` (Avalonia's measure caps the viewport at its `MaxHeight` and scrolls the rest), and the third carries no `max-height`.

*Precedent: shadcn's `CommandList` (`max-h-[300px] overflow-y-auto`).*

### LAYOUT-89: A flow direction is written where it changes

- **Given** a panel whose `FlowDirection` is `RightToLeft` holding a text that inherits it and a horizontal panel set back to `LeftToRight`, and a text outside it
- **When** the page is rendered
- **Then** the right-to-left panel carries `dir="rtl"`, the panel set back carries `dir="ltr"`, and no other element carries `dir`, so the browser lays the panel's flex rows, grid columns, logical alignments and bidirectional text out mirrored where Avalonia mirrors them.

*Precedent: Avalonia's `Visual.FlowDirection` (inherited, a control mirrored where its direction differs from its parent's, `HasMirrorTransform`), HTML's `dir` attribute (inherited, CSS Writing Modes 4's `direction`), shadcn/ui's RTL examples setting `dir` on a wrapper.*

### LAYOUT-90: A scroll fade is written as shadcns utility

- **Given** scroll viewers marked `a:Web.ScrollFade` `y`, `x`, `b` and an unknown value
- **When** the page is rendered
- **Then** the first three carry the classes `sfy`, `sfx` and `sfb` and the fourth none; the style sheet holds once the registered `--sf-t`, `--sf-b`, `--sf-s` and `--sf-e` lengths and their keyframes, and for each class shadcn/ui's `scroll-fade` utility: a mask gradient transparent at its edges and opaque between `var(--sf-t)` and `calc(100% - var(--sf-b))` (the start and the end for `x`, turned `to left` in a right-to-left element, one edge for `b`), each edge's length animated from 0 to `min(12%, 40px)` over the first or last 96 px of the scroll along its axis (`y` or `inline`) where the browser has scroll timelines, and the whole length where it has none.

*Precedent: the shadcn package 4.21.5's `scroll-fade-y`, `scroll-fade-x` and `scroll-fade-b` utilities (`dist/tailwind.css`), CSS Masking's `mask-image`, Scroll-driven Animations' `scroll` timelines.*

### LAYOUT-91: Height limits are written

- **Given** a `ContentControl` with a `MaxHeight` of 192 holding a scroll viewer of taller content, a `Grid` with a `MinHeight` of 240 holding a button at its bottom, and a `Border` with no limit
- **When** the page is rendered
- **Then** the content control's element carries `max-height:192px`, the grid's `min-height:240px` and the border's neither, so the scroll viewer scrolls within 192 px and the button stands 240 px down as Avalonia lays them out.

*Precedent: Avalonia's `Layoutable.MinHeight` and `MaxHeight` (the measure clamped between them), CSS's `min-height` and `max-height`, Tailwind's `max-h-*` and `min-h-*` on shadcn/ui's code block (`max-h-48`) and chat examples (`min-h-72`).*

### LAYOUT-92: A templates parts take their state looks

- **Given** a `ContentControl` whose own theme's template holds a box and a hidden part, a `:pointerover` style showing the part and a `:focus-within` style tinting the box
- **When** the page is rendered
- **Then** the control's element carries a state class whose rules give the part `opacity:1` under `:hover` and the box its tint under `:focus-within`, as Avalonia shows them.

*Precedent: Avalonia's control themes (`^:pointerover /template/ Part` styles), shadcn/ui's `group-hover` and `group-focus-within` (the code block's floating actions), Aioros's themed date picker parts (the same registration).*

### LAYOUT-93: A scroll fade on a viewer following its end is reversed

- **Given** a scroll viewer marked `a:Web.ScrollFade` `y` that follows its end (`a:Web.FollowsEnd`) and one that does not
- **When** the page is rendered
- **Then** the first carries the class `sfyr` whose top fade shrinks from `min(12%, 40px)` to 0 over the last 96 px of its scroll timeline and whose bottom fade grows from 0 over the first 96 px (`asftr`, `asfbr`), as its element is written from its end and its timeline starts at its bottom, so the bottom stays opaque while it rests at its end; the second keeps `sfy`.

*Precedent: shadcn/ui's message scroller (`scroll-fade-b` hidden at the end), CSS Flexbox's `column-reverse` (the scroll origin at the end), Scroll-driven Animations' `scroll` progress from the scroll origin.*

### LAYOUT-96: Library shells and outlets pass the viewports height

- **Given** the window-filling shell of LAYOUT-54 built from a library's controls, as Aioros.Cosmo's dashboard is: a library panel written as a dock (`a:Web.Layout="Dock"`, its side docked at the left and the rest filling), a library content control that gives its content its own height (`a:Web.Layout="Fill"`, as Cosmo's `c:SidebarInset`), and a layout's `a:Outlet` holding the page's `ScrollViewer`
- **When** the page is rendered
- **Then** the document is a viewport shell as in LAYOUT-54: each of those elements fills its parent (`vf`) and the page's `ScrollViewer` is the scroll container (`vs`), so the sidebar stays as tall as the window while the page scrolls, as Avalonia lays them out on the desktop; the same library content control without `a:Web.Layout="Fill"` breaks the chain (its template is its own, so its height is not known to pass) and makes no shell.

### LAYOUT-97: Stacked uniform grids flow their items in one column

- **Given** a `UniformGrid` with `Columns="4"` and `a:Web.StackBelow="900"` as an `ItemsControl`'s panel, and one with `Rows="1"` and `a:Web.StackBelow="600"`
- **When** the page is written for the web
- **Then** each grid carries its stack class (`sb900`, `sb600`) as a `Grid` with the hint does, and below that width its items take the single column and flow in rows (`grid-auto-flow:row`), the one-row grid included

*Precedent: LAYOUT-49's rule for `Grid`; Tailwind's `grid-cols-1` below a breakpoint.*

### LAYOUT-98: Stacked grids keep a gap between their cells

- **Given** a `Grid` with two columns, `a:Web.StackBelow="640"` and `ColumnSpacing="16"` and no `RowSpacing`, one with `RowSpacing="8"` as well, and one with neither
- **When** each is laid out in a browser 400 px wide
- **Then** the second cell follows the first with a gap of 16 px, 8 px and 0 px respectively: below the width the columns are stacked into, the column spacing separates the stacked cells unless the grid sets a row spacing, which it keeps (LAYOUT-49).

*Precedent: a CSS grid's `gap`, which holds both axes and keeps separating items when a media query leaves one column.*

## Split script

### LAYOUT-62: Separators resize their tracks with the pointer and the keys

- **Given** a grid of three star columns with a separator as LAYOUT-59 writes it (step 5%, collapsing to 0, from 10% to 75%) and a second with a 10 px step
- **When** the first is dragged with the pointer by 40 px
- **Then** the first column grows by 40 px, the second shrinks by as much and `aria-valuenow` follows; dragged far it stops at 75%; then ArrowLeft moves it by 5%, ArrowUp moves nothing, Home and End move it to 10% and 75%, Enter collapses the first column to 0% and Enter again restores it, F6 moves the focus to the second separator and Shift+F6 back (wrapping), and ArrowRight on the second moves it by 10 px.

*Precedent: the WAI-ARIA window splitter pattern (arrows, Home and End, Enter to collapse and restore); react-resizable-panels 4.5.8 (pointer capture, F6 to the next separator of the group); Avalonia's `GridSplitter` drag.*

### LAYOUT-61: Moved separators keep their sizes across an answer

- **Given** a page whose separator was moved in the browser
- **When** a post's answer morphs the page
- **Then** the grid keeps its new tracks and the separator its `aria-valuenow`, rather than the rendered ones; the sizes are not posted (Avalonia's `GridSplitter` writes the definitions' lengths, but neither a definition bound to the model nor a library's size property is followed by the plan).

*Precedent: react-resizable-panels' layout kept by the client between renders (its `autoSaveId` storage); LiveView's `phx-update="ignore"` for client-owned attributes.*

### LAYOUT-87: A delayed separator posts its position once after a drag

- **Given** a separator marked `data-change data-delay="150"` holding its hidden field and a second separator holding one without
- **When** the visitor drags the first with the pointer and moves the second with an arrow key
- **Then** each field reads its separator's new `aria-valuenow`, the first is posted as a change (`_chg`) once, 150 ms after the drag ends, and the second waits for the form's next post.

*Precedent: the delayed text box (`data-delay`, LiveView's `phx-debounce`); the WAI-ARIA window splitter.*

## Shell

### LAYOUT-63: Library panels docked on the web

- **Given** a library's panel that arranges its children in its own code (Aioros.Cosmo's `c:SidebarProvider`: its sidebar at one side at its own width, the rest beside it) marked `a:Web.Layout="Dock"`, holding a 240 px side docked `Left` and a main area, and a second such panel whose side is docked `Right`, and a third without the hint
- **When** they are rendered
- **Then** the first two are written as a `DockPanel` is (LAYOUT-08): a grid of `auto minmax(0,1fr)` columns (`minmax(0,1fr) auto` for the right side) with the side in its own column at its width and the main area in the filling one, so neither covers the other; the third stays a panel whose children share one cell (`grid-area:1/1`).

### LAYOUT-64: Fixed widths in a row do not shrink

- **Given** a horizontal `StackPanel` inside a 32 px wide border that clips, holding a `Viewbox` 16 px wide and a text
- **When** it is rendered
- **Then** the viewbox's element keeps its 16 px (`flex-shrink:0`), as Avalonia's horizontal `StackPanel` measures its children with an unbounded width and never shrinks one with a width of its own; the text, with no width of its own, keeps its natural width as well, by its stack's rule rather than a style of its own (LAYOUT-71, LAYOUT-72), and is clipped by the border, as Avalonia clips it.

### LAYOUT-65: A scroll viewers thumb is its own

- **Given** a scroll viewer of its own theme (LAYOUT-60) whose bars' thumb is `#e5e5e5`, holding a Fluent scroll viewer of its own with a visible bar before its own bars in the visual tree (Aioros.Cosmo's Workspace inset holding its data table)
- **When** it is rendered
- **Then** its `scrollbar-color` is its own thumb's (`#e5e5e5 transparent`), never the nested viewer's, so the written look does not depend on which bars a nested list happened to realize first.

### LAYOUT-85: Bars hidden at rest show under the pointer and hide after their delay

- **Given** a scroll viewer of its own theme (LAYOUT-60) whose bars are hidden at rest (`Opacity` 0, shown while the viewer is under the pointer, as Aioros.Cosmo's scroll area and Radix's `type="hover"`) with `a:Web.ScrollbarHideDelay="600"`, a second such viewer without the hint, and a third whose bars show at rest
- **When** the page is rendered
- **Then** the first's thin bars are transparent at rest with a `scrollbar-color` transition delayed 600 ms and show the thumb's color under `:hover` at once (`transition-delay:0s`), the second's hide with no delay, and the third keeps the thumb's color at rest with no hover rule; in Chromium the first's computed `scrollbar-color` is transparent at rest and the thumb's under the pointer.

*Precedent: Radix ScrollArea's `type="hover"` and `scrollHideDelay` (600 ms), CSS Scrollbars' `scrollbar-color`, which Chromium transitions with a delay.*

## Slot layout

### LAYOUT-70: An aligned box is never wider than its slot

- **Given** a page 390 px wide holding a left-aligned `StackPanel` with `MaxWidth="880"` and a centered `StackPanel` with a 12 px margin on each side, each over a wrapping `TextBlock` of a sentence longer than the page and a `TextBlock` 560 px wide whose text is aligned right (Aioros.Cosmo's Gallery page column over a demo wider than a phone)
- **When** the page is laid out in the browser and by Avalonia
- **Then** each column is no wider than its slot, as Avalonia's `Layoutable.ArrangeCore` arranges an aligned element at the smaller of its slot and its desired size (written `max-width:min(880px,100%)`, less its margins): both sentences wrap inside the page (the centered one inside its margins), and the 560 px blocks keep their width, centered on their column and overflowing it on both sides as Avalonia arranges a stretched block of a fixed width (their text ending at 475 px), where a column grown to its widest child would end them at 560 and 572 px. A box of a fixed width, and the content of a scroll viewer, which Avalonia arranges at its extent, are left as they were.

## Row layout

### LAYOUT-71: A horizontal stacks children keep their width

- **Given** a page 300 px wide holding a horizontal `StackPanel`, stretched across it, of seven bordered items with 4 px between them, the first holding "<" and "Previous" and the last "Next" and ">" in horizontal stacks of their own (Aioros.Cosmo's pagination on a phone), together wider than the page
- **When** the page is laid out in the browser and by Avalonia
- **Then** every item keeps its natural width and no text runs into the next (at least 3 px between them), as Avalonia's `StackPanel` arranges each child of a horizontal stack at its desired width whatever the room (`StackPanel.ArrangeOverride`, 12.1.3), written `flex-shrink:0` for the stack's children by one rule of the stack (LAYOUT-72), where the browser would shrink them and let their text overlap.

### LAYOUT-72: A horizontal stack keeps its children widths without wrappers

- **Given** an `ItemsControl` whose items panel is a horizontal `StackPanel` with 4 px between its items, over two strings shown by a template of a bordered text (Lumen's label pills on `/projects`)
- **When** the page is rendered
- **Then**
  - The stack carries the shared class `hs`, whose one rule keeps its children at their width (`.c.hs>*` and the children of its `display:contents` items, `flex-shrink:0`, LAYOUT-71), and each item is written as before that rule: the item's border is its element, with no element for the item's content presenter.
  - Writing `flex-shrink:0` on each child instead gave every content presenter an element of its own, which made Lumen's `/projects` 2.4 KB larger and 9% slower (CI bench run 240).

## Scroll layout

### LAYOUT-80: A scroll viewer with a hidden bar scrolls without it

- **Given** a `ScrollViewer` 384 px wide whose horizontal bar is `Hidden` and vertical bar `Disabled` (Avalonia scrolls a hidden bar's axis without showing the bar, as `CanHorizontallyScroll` is true for every visibility but `Disabled`; Aioros.Cosmo's attachment group, shadcn/ui's `overflow-x-auto scrollbar-none`), holding a horizontal stack of three 300 px boxes, of Fluent's theme and of a library's own theme that frames its presenter in a `Border`, and a third viewer whose horizontal bar is `Hidden` and vertical bar `Auto`
- **When** they are rendered
- **Then** the first two scroll horizontally (`overflow-x:auto`) with no bar (`scrollbar-width:none`), and in Chromium their content keeps its 932 px and scrolls to its end; the third scrolls horizontally too and keeps its bars, since one of its scrolling axes shows a bar.

## Container query

### LAYOUT-81: A container query is written as a container rule

- **Given** a `Border` named as a container (`Container.Name="card"`, `Container.Sizing="Width"`) with 16 px of padding, holding a text hidden and a text given a 24 px left margin by a `ContainerQuery Name="card" Query="max-width:500"` in its styles
- **When** the page is written at the compile width and laid out at 400, 490, 510 and 800 px
- **Then** its element is an inline-size container named for it, the query's looks are written under `@container` with the query's width less the container's padding and border (`max-width:468px`, as CSS measures a container's content box and Avalonia its whole box), and Chromium shows the same texts at the same places as Avalonia at each width.

### LAYOUT-82: Combined queries and queries holding when written follow the container

- **Given** a container whose `ContainerQuery` combines widths (`min-width:600 and max-width:900, max-width:300`) and shows a text hidden outside it, and a second query holding at the compile width (`min-width:1000`) that hides another
- **When** the page is written and laid out at 250, 450, 700, 950 and 1100 px
- **Then** the text hidden at the compile width is written hidden and shown under the query, the query that holds at the compile width is written as `not (…)` around the looks it does not give, and Chromium shows the same texts at the same places as Avalonia at each width.

### LAYOUT-83: An aligned container takes its slot

- **Given** a container aligned left with a 500 px `MaxWidth` holding a right-aligned text and a query without a name (`max-width:450`, the nearest container's, as Avalonia's)
- **When** the page is laid out at 400 and 800 px
- **Then** the container takes its whole slot up to its `MaxWidth`, as Avalonia measures a container at its available width, rather than its content's width, and Chromium ends the text at the same place as Avalonia at each width (its end, which the alignment fixes, rather than its start, which also depends on the font's metrics).

### LAYOUT-84: A nested container follows the queries around it

- **Given** a container named `block` whose `max-width:450` query sets the margin of a container nested in it (named `inner`, as Aioros.Cosmo's `c:FieldGroup` inside its blocks' frame), and the nested container's own `min-width:300` query giving a text inside it a left margin and setting the nested container's own padding
- **When** the page is laid out at 300, 400 and 800 px (the outer query holding with the inner one failing, both holding, the outer failing)
- **Then** the nested container's margin follows only the outer query and its padding never changes, since a container query selects the nearest ancestor container and never the element itself (Avalonia's `ContainerQueryActivatorBase.GetContainer` walks `GetVisualAncestors`; CSS Containment 3's query container selection excludes the element), the text's margin follows the inner query in each state of the outer one, and Chromium shows the texts at the same places as Avalonia at each width.

## Aspect ratio

### LAYOUT-94: An aligned ratio takes its slot up to its max width

Given, in a 510 px column, borders with `a:Web.AspectRatio="1.7778"` and no width of their own (a library panel that keeps a ratio in its own layout, Aioros.Cosmo's `c:AspectRatio` as sidebar-13's tiles), one aligned left with a `MaxWidth` of 768 and one aligned left with a `MaxWidth` of 300, when the page is rendered, then each is as wide as its slot up to its `MaxWidth` (`width:100%` beside its `max-width`), at its start, so Chromium lays them out 510 by 287 px and 300 by 169 px as Avalonia does, never collapsing to the width of its empty content.

*Precedent: Radix's `AspectRatio` (a `width:100%` wrapper), Avalonia's `ArrangeCore` (a left-aligned control arranged at its desired size, which a ratio panel measures as its slot).*

## Dock spacing

### LAYOUT-95: A dock panel without a filling child has no spacing after its last

- **Given** a `DockPanel` with `VerticalSpacing="8"`, `HorizontalSpacing="8"` and `LastChildFill="False"` holding two 20 px borders docked at the top and one docked at the bottom, sized by its content, and the same panel 200 px tall, and a row of the same kind with two 20 px borders docked left and one right
- **When** Chromium lays them out
- **Then** the first is 76 px tall with its bottom border at 56 px (one spacing between neighbours, none after the last, as Avalonia measures a dock panel without a filling child), the second keeps its tops at 0 and 28 px and its bottom border at 180 px, and the row is 76 px wide.

*Precedent: Avalonia's `DockPanel.MeasureOverride` (the spacing after the last docked child taken off when no child fills).*
