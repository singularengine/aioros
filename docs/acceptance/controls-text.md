# Controls: text

Text display and text entry (`src/Adapters/Aioros.Avalonia/Html/Families/TextFamily.cs`).

50 criteria. Each has an identifier and exactly one test.

## Text controls

### TEXT-01: Text blocks are text elements

- **Given** a `TextBlock`
- **When** it is rendered
- **Then** its text is written encoded in a `div`, or in an `h1` or `h2` when it has that class, with its font size, weight, wrapping, trimming and alignment as CSS; spaces and line breaks are kept as Avalonia keeps them (`white-space:pre` without wrapping, `pre-wrap` with it).

### TEXT-02: Text boxes are inputs

- **Given** a `TextBox`
- **When** it is rendered
- **Then** it is an `<input>` named after its field id with its text as the value and its placeholder; with `AcceptsReturn` it is a `<textarea>`, and `a:Web.InputType` sets the input type.

### TEXT-03: Posted text reaches the model

- **Given** a posted value for a `TextBox`
- **When** the post is applied
- **Then** the bound property receives it; a field missing from the post leaves the text unchanged.

### TEXT-04: Inlines are written in order

- **Given** a `TextBlock` with inlines
- **When** it is rendered
- **Then** they are written in order inside it: a `Run` as its encoded text (a bound `Run` is a plan slot), `Bold` as `<b>`, `Italic` as `<i>`, `Underline` as `<u>`, a `Span` as a `<span>` whose locally set font size, weight, style, color and decorations are a CSS class, a `LineBreak` as `<br>`, and an `InlineUIContainer` as a `<span>` holding its child control.

### TEXT-05: Selectable text is selectable

- **Given** a `SelectableTextBlock`
- **When** it is rendered
- **Then** it is written like a `TextBlock` with `user-select:text`, a text cursor, and its `SelectionBrush` as the `::selection` background.

### TEXT-06: Access key markers are removed

- **Given** an `AccessText` whose text marks an access key with `_`
- **When** it is rendered
- **Then** the marker is removed (`_Save` reads `Save`) and a doubled `__` reads as one literal underscore.

### TEXT-07: Labels point at their target

- **Given** a `Label` whose content marks an access key and whose `Target` is a control with `a:Web.Id`
- **When** it is rendered
- **Then** it is a `<label for>` naming that id with the key as its `accesskey`, its content without the marker and its Fluent padding, and the target `TextBox` carries that `id`; a `Label` without a target has no `for`.

### TEXT-08: Text box variants are input attributes

- **Given** `TextBox` variants
- **When** they are rendered
- **Then** a `PasswordChar` makes `type="password"`, `MaxLength` is `maxlength`, `IsReadOnly` is `readonly`, a disabled box is `disabled`, `PlaceholderText` (or the obsolete `Watermark`) is the `placeholder`, which is omitted when empty, and a multi-line box that does not wrap is `wrap="off"`.

### TEXT-09: Text boxes look like fluent

- **Given** a `TextBox` in the Fluent theme
- **When** it is rendered
- **Then** it carries the background, border, corner radius, padding and minimum height of its `PART_BorderElement`, `:hover` and `:focus` classes with Fluent's pointer-over and focused colors, the thicker focused border drawn as an inset shadow so the text does not move, and its placeholder color as a `::placeholder` rule.

### TEXT-10: Masked text boxes are pattern inputs

- **Given** a `MaskedTextBox`
- **When** it is rendered
- **Then** it is a text `<input>` whose `pattern` is the mask translated to a regular expression and whose `placeholder` is the mask with its prompt characters; its `value` is empty until a position is filled, then the formatted text.

### TEXT-11: Posted masked text goes through the mask

- **Given** a posted value for a `MaskedTextBox`
- **When** the post is applied
- **Then** the text goes through the mask and the bound property receives the formatted text; a field missing from the post leaves it unchanged.

### TEXT-12: Numeric up downs are number inputs

- **Given** a `NumericUpDown`
- **When** it is rendered
- **Then**
  - It is an `<input type="number">` with `min` and `max` from `Minimum` and `Maximum` (omitted at the decimal limits) and `step="any"`, because Avalonia accepts any value between the limits (`Increment` only steps the spin buttons, and an HTML `step` would block the form on a value off its grid) and its value in invariant culture, with the Fluent text box look
  - `IsReadOnly` is `readonly` and `ShowButtonSpinner="False"` hides the spin buttons.

### TEXT-13: Posted numbers reach the value

- **Given** a posted value for a `NumericUpDown`
- **When** the post is applied
- **Then** an invariant number becomes the bound `Value`; a number outside `Minimum` and `Maximum` is clamped with `ClipValueToMinMax` and otherwise rejected as Avalonia does; an empty value clears it, and an unparseable or missing value leaves it unchanged.

### TEXT-14: Auto complete boxes are datalist inputs

- **Given** an `AutoCompleteBox`
- **When** it is rendered
- **Then** it is a text `<input>` with its text as the value, its placeholder and a `list` naming a `<datalist>` whose options are its items, written once per page for the same items; a bound `ItemsSource` is a plan slot that repeats the options.

### TEXT-15: Posted auto complete text reaches the model

- **Given** a posted value for an `AutoCompleteBox`
- **When** the post is applied
- **Then** the bound `Text` receives it; a field missing from the post leaves it unchanged.

### TEXT-16: Lines are as tall as Avalonia lays them out

- **Given** `TextBlock`s at 11, 14, 18, 20 and 28 px stacked in a `StackPanel`
- **When** the page is rendered
- **Then** its style sheet sets every element's `line-height` to Avalonia's line spacing for its default font (ascent plus descent plus line gap over the em, `TextLineImpl.CreateLineMetrics`) times the font size, rounded up to a whole pixel (`round(up,…em,1px)`) as Avalonia's layout rounding rounds a `TextBlock`'s size up, so each text is as tall in the browser as in Avalonia's layout (17, 22 and 34 px at 14, 18 and 28 px, where the browser's `line-height:normal` gives 17, 21 and 34) and the next text starts where it starts in Avalonia.

### TEXT-17: Text block padding insets the text

- **Given** a `TextBlock` with `Padding="10,4"` in a `Border` without a width
- **When** it is rendered
- **Then** the text element has `padding:4px 10px 4px 10px`, so its text starts where Avalonia lays it out (10 px right of the border and 4 px down) and the border is as wide as the text plus the padding.

### TEXT-18: Color pickers are color inputs

- **Given** a `ColorPicker` whose `Color` is bound two-way
- **When** it is rendered
- **Then** it is an `<input type="color">` named after its field whose `value` is the color as `#rrggbb` (the browser's color input has no alpha), sized as Avalonia lays out the picker's button, and the plan is reusable and writes another model's color
- **When** a post names `#rrggbb`
- **Then** the bound color takes those channels and keeps its alpha, and a posted value that is not a color leaves it unchanged.

### TEXT-28: Labels show their bound text

- **Given** a `Label` whose `Content` is bound to a text
- **When** the page is planned
- **Then** the plan is reusable and writes the model's text as Avalonia's label shows it, the access key marker taken out (`_Call` is `Call`, `__` is `_`) and nothing for a null text, byte for byte as Avalonia's render of each model; a bound `Label` with a `Target` is planned too: it points at its target (`for`) and writes no `accesskey`, which would follow the text (the desktop keeps the access key; Cosmo's need 23, 2026-10-16).

### TEXT-29: Color pickers post through the plan

- **Given** a `ColorPicker` whose `Color` is bound two-way
- **When** the page is planned
- **Then** its field is described to the plan (`color` and the bound path), and a post through the plan's fields (not the Avalonia engine) that names `#rrggbb` gives the bound color those channels with the alpha it had, while a value that is not a color leaves it unchanged, from the plan and from the plan read back from a plans file.

### TEXT-30: Font families and their resource faces are written

- **Given** a `StackPanel` with `TextElement.FontFamily="Courier New, Courier"` holding a `TextBlock` that inherits it and a `TextBlock` whose family comes from a folder of an assembly's Avalonia resources (`avares://Avalonia.Fonts.Inter/Assets#Inter`)
- **When** the page is rendered
- **Then** the panel's element has `font-family:"Courier New","Courier",Inter,system-ui,sans-serif` (Avalonia's default family as the fallback), the inheriting text has no `font-family` of its own, the other text has `font-family:"Inter",…`, and the style sheet declares an `@font-face` for each font file of that folder whose family is the one named, its `src` at `/_aioros/fonts/r/Avalonia.Fonts.Inter/Assets/Inter-Regular.ttf` (`truetype`) and its `font-weight` read from the file (250 for `Inter-Thin.ttf`, as its OS/2 table says, and 700 for `Inter-Bold.ttf`); a page that never changes the family writes no `font-family`.

### TEXT-31: Resource files are read without Avalonia

- **Given** an assembly with Avalonia resources (`Avalonia.Fonts.Inter`)
- **When** a resource file is read through `AvaloniaResourceFiles` (the web host's `/_aioros/fonts/r/{assembly}/{path}`)
- **Then** its bytes are the ones Avalonia's `AssetLoader` opens for its `avares://` URI, read without Avalonia; a path the assembly does not hold reads as nothing.

### TEXT-32: Auto complete boxes hide the browsers drop down indicator

- **Given** an `AutoCompleteBox`
- **When** it is rendered
- **Then** the page's style sheet hides the indicator Chromium draws in an `<input list>` on hover and focus (`input[list]::-webkit-calendar-picker-indicator{display:none!important}`), as Avalonia's `AutoCompleteBox` has no drop-down button; the suggestions still open as the user types.

### TEXT-33: Label line height of the template is written

- **Given** a `Label` whose template sets `TextBlock.LineHeight="14"` on its content presenter (Aioros.Cosmo's), and a Fluent `Label`
- **When** the page is rendered
- **Then** the themed `<label>` has `line-height:14px`, so its text sits on Avalonia's line, and the Fluent one keeps its font's line height.

### TEXT-39: Letter spacing and font style are written

- **Given** a `TextBlock` whose style sets `LetterSpacing="-0.9"`, a `TextBlock` whose style sets `FontStyle="Italic"`, a templated control's `ContentPresenter` part with `LetterSpacing="-0.45"` showing a string, a `TemplatedControl` with `LetterSpacing="1.2"` whose text inherits it, and a plain `TextBlock` (shadcn/ui's `tracking-tight` and the blockquote's italic in Aioros.Cosmo)
- **When** the page is rendered
- **Then** the texts' elements carry `letter-spacing:-0.9px`, `font-style:italic`, `letter-spacing:-0.45px` and `letter-spacing:1.2px`, as Avalonia adds the spacing after each character and draws the italic face, and the plain text carries neither.

### TEXT-40: Max lines clamp the text

- **Given** a wrapping `TextBlock` with `MaxLines="2"` and `TextTrimming="CharacterEllipsis"`, a wrapping one with `MaxLines="2"` and no trimming, and one with `MaxLines="2"` that does not wrap
- **When** the page is rendered
- **Then** the first is clamped to two lines with an ellipsis (`display:-webkit-box;-webkit-box-orient:vertical;-webkit-line-clamp:2;overflow:hidden`, Tailwind's `line-clamp-2`), the second is cut after two lines without one (`max-height:2lh;overflow:hidden`), and the third gets neither.

### TEXT-41: A text box posts as it is typed after its web delay

- **Given** `TextBox`es bound two way to the model: one with `a:Web.Delay="250"` and no binding delay, one whose binding has `Delay=200` and no hint, one with both (`a:Web.Delay="400"` over `Delay=200`), one with `a:Web.Delay="0"` over `Delay=200`, and one with neither
- **When** the page is rendered
- **Then**
  - The first `<input>` carries `data-change data-delay="250"`, the second `data-delay="200"`, the third `data-delay="400"` (the hint, which speaks for the web, wins over the binding), and the last two neither attribute (a hint of 0 says the web does not post the text as it is typed).
  - When a post binds a value into each, every model member holds it at once: the hint leaves Avalonia's binding alone, so the native targets update the model on every keystroke, and a binding's own `Delay` updates its source as the post binds it, since the post is sent once the visitor paused.

*Precedent: WPF's and Avalonia's `Binding.Delay`; LiveView's `phx-debounce`, set on the input for the browser alone; Uno's platform XAML namespaces and Aioros's own `a:Web.*` hints, which the native targets ignore.*

### TEXT-42: Linked inlines are links in their text

- **Given** a `TextBlock` whose inlines are a `Run`, a `Run` with `a:Web.Href="/signup"` and `TextDecorations="Underline"`, and a `Span` with `a:Web.Href="/terms"` holding a `Bold` (shadcn/ui's `Don't have an account? <a href="#" class="underline">Sign up</a>`)
- **When** the page is rendered
- **Then** each linked inline is an `<a href>` inside the text (marked current as any `a:Web.Href` link is), holding the inline as it is written without an address, its color and its underline taken from the text unless the inline sets its own (`:where(a.il){color:inherit;text-decoration:inherit}`), and the other runs stay plain text.

*Precedent: WinUI's and WPF's `Hyperlink` inline with its `NavigateUri`, .NET MAUI's `Span` with a tap gesture, Flutter's `TextSpan` with a recognizer.*

### TEXT-43: Composite font families declare their faces

- **Given** a `TextBlock` whose `FontFamily` lists two families of assemblies' Avalonia resources (`avares://Aioros.Avalonia.Tests/Assets/Fonts#Cosmo Symbols, avares://Avalonia.Fonts.Inter/Assets#Inter`, Avalonia's composite font, which takes a glyph the first family lacks from the next, as a library pairs its text font with a font of the symbols it lacks)
- **When** the page is rendered
- **Then** its `font-family` names both in order before Avalonia's default (`"Cosmo Symbols","Inter",Inter,system-ui,sans-serif`), and the style sheet declares an `@font-face` for the font files of each family, as it does for one (TEXT-30), so the browser falls back glyph by glyph as Avalonia does.

### TEXT-44: One line fields without a width size to their text

- **Given** a one-line `TextBox` without a width showing its placeholder `<M/d/yyyy>` (Avalonia sizes it to the text it shows, at least its `MinWidth`, as in Semi.Avalonia's date picker field) and one with `Width="200"`
- **When** the page is rendered
- **Then** the first `<input>` sizes to its value or placeholder (`field-sizing:content`) with its `min-width`, not to the browser's 20 characters, while a stretched field still fills its slot, and the second keeps its 200 px.

### TEXT-45: Color fonts name their technology

- **Given** a `TextBlock` whose `FontFamily` is a resource font of color bitmaps (`avares://Aioros.Avalonia.Tests/Assets/Fonts#Cosmo Emoji`, Noto Color Emoji's `CBDT` and `CBLC` tables, as Aioros.Cosmo pairs its emoji with its text font), and one in an outline font (Cosmo Symbols)
- **When** the page is rendered
- **Then**
  - The color font's `@font-face` source names its color technology (`format('truetype') tech(color-CBDT)`, and likewise `color-COLRv0`, `color-COLRv1`, `color-sbix` and `color-SVG` for those tables), so a browser that does not draw it, as Firefox does not draw `CBDT`, skips the source without loading it and falls back to the next family, rather than rejecting the file with a console error; the outline font's source names no technology.
  - CSS Fonts 4's `tech`.

### TEXT-52: Text wrap hints are written as CSS text wrap

- **Given** `TextBlock`s with `a:Web.TextWrap="balance"`, `a:Web.TextWrap="pretty"` and `a:Web.TextWrap="bogus"`
- **When** the page is rendered
- **Then** the first's class carries `text-wrap:balance`, the second's `text-wrap:pretty`, and the third carries no `text-wrap`, and a `ContentControl` with `balance` passes it down to the text it presents (the property is inherited, as CSS's `text-wrap` is), as CSS Text 4's `text-wrap` (shadcn/ui's `text-balance` on an empty state's and a horizontal field's description); the hint is ignored on the native targets, where a library balances the lines itself.

### TEXT-54: A shimmering text is written as shadcns shimmer

- **Given** a `TextBlock` and a `ContentControl` with `a:Web.Shimmer="True"` and a `TextBlock` without it
- **When** the page is rendered in the light theme and in the dark one
- **Then** the shimmering elements carry the `shm` class in the light theme (`shmd` in the dark) and the plain text neither; the style sheet holds once shadcn/ui's `shimmer` utility: a 110° gradient of `currentColor` to a highlight of the text's color at a fifth of its alpha in the light theme (lightness at least 0.8 and alpha plus 0.4 in the dark) and back, `calc(200% + 2 × (3ch + 40px))` wide, clipped to the text and moved from `100% 0` to `0 0` over 2 s, linear and endless, reversed in a right-to-left element, and no animation or gradient for a visitor who asks for less motion.

*Precedent: the shadcn package 4.21.5's `shimmer` utility (`dist/tailwind.css`), CSS Backgrounds 4's `background-clip: text`.*

### TEXT-55: An underline moved by its stroke offset is written at the same distance

- **Given** 16 px inlines whose underlines are moved 3 px by their `StrokeOffset` in pixels, one 2 px thick and one 4 px thick, a 16 px `TextBlock` whose own 2 px underline is moved the same, a `TextBlock` struck through, an `Underline` inline left at the font's recommended place, and a `HyperlinkButton` styled with an underline of the font's thickness moved a quarter of its 16 px font by `StrokeOffset` in `FontRenderingEmSize`
- **When** the page is rendered
- **Then** each moved inline is written with `text-underline-offset` at the top of the line Avalonia draws: the font's underline position below the baseline plus 3 px less half the line's thickness (so the 4 px line stands 1 px above the 2 px one), the `TextBlock`'s at the 2 px inline's, and the struck text with `text-decoration:line-through`; the plain underline is written with none (the browser's `auto`, the font's own place); and the link's look with the font's underline position plus 4 px less half the font's underline thickness.

*Precedent: Avalonia 12.1's `TextDecoration.Draw` (a pen of the decoration's thickness centered at the baseline plus the face's `UnderlinePosition` plus the offset in its unit), CSS Text Decoration 4's `text-underline-offset` (a length placing the line's top edge from the alphabetic baseline).*

### TEXT-56: An embedded control stands as its baseline alignment says

- **Given** a `TextBlock` holding a run and three `InlineUIContainer`s, the first on the default `BaselineAlignment`, the second `Center` and the third `Top`
- **When** the page is rendered
- **Then** each container is an inline flex box aligned by `vertical-align` `baseline`, `middle` and `top`: the default one stands on the line's baseline, by its bottom as Avalonia's `EmbeddedControlRun` sets a control (by its text's baseline where it holds text, as CSS aligns an inline box).

*Precedent: Avalonia's `Inline.BaselineAlignment` (default `Baseline`) and `EmbeddedControlRun`, CSS 2.1's `vertical-align` and the baseline of an inline flex container (CSS Flexbox 1, 8.5).*

### TEXT-57: Font features are written

- **Given** a text with `FontFeatures` `+tnum`, one with `+tnum, -liga` and a plain text
- **When** the page is rendered
- **Then** the first carries `font-feature-settings:"tnum" 1`, the second `"tnum" 1,"liga" 0` and the plain text none, so its digits take the widths Avalonia gives them.

*Precedent: Avalonia's `TextElement.FontFeatures` (HarfBuzz's feature strings), CSS Fonts 4's `font-feature-settings`, Tailwind's `tabular-nums` on shadcn/ui's questionnaire progress.*

### TEXT-58: Fallbacks after the inherited family are written

- **Given** a `StackPanel` whose `TextElement.FontFamily` is Avalonia's composite font `avares://Aioros.Avalonia.Tests/Assets/Fonts#Cosmo Symbols, avares://Avalonia.Fonts.Inter/Assets#Inter`, holding a `TextBlock` that inherits it, a `TextBlock` that sets the same list again, and a `TextBlock` whose list starts with the same family and goes on with another (`…#Cosmo Symbols, avares://Aioros.Avalonia.Tests/Assets/Fonts#Cosmo Emoji`)
- **When** the page is rendered
- **Then** the third names its own list (`"Cosmo Symbols","Cosmo Emoji",Inter,system-ui,sans-serif`) and the style sheet declares Cosmo Emoji's face, while the first two write no `font-family`: a family is compared with the one it inherits by every family it lists, not by its first alone, so a fallback added after the inherited text font reaches the browser as Avalonia uses it (Aioros.Cosmo's `code-block-rtl`, whose Arabic font follows Geist).

### TEXT-59: Relative font sources are resolved against their file

- **Given** a compiled resource dictionary of the app's assembly holding a `FontFamily` with a relative source (`/Assets/Fonts#Cosmo Emoji`) and a composite font whose second family is relative (`avares://Avalonia.Fonts.Inter/Assets#Inter, /Assets/Fonts#Cosmo Emoji`), as Avalonia resolves them against the XAML file that names them, and a page parsed at run time whose `TextBlock` names a relative source with no file to resolve it against
- **When** the page is rendered
- **Then** the first two `TextBlock`s name their families (`"Cosmo Emoji",Inter,…` and `"Inter","Cosmo Emoji",Inter,…`) and the style sheet declares Cosmo Emoji's face from the app's assembly (`/_aioros/fonts/r/Aioros.Avalonia.Tests/Assets/Fonts/CosmoEmoji-Regular.ttf`), and the third names its family with no face instead of failing the page.

## Field theme

### TEXT-34: Field shadows are written at rest and in their states

- **Given** a `TextBox` whose control theme gives `PART_BorderElement` a `BoxShadow` at rest (`0 1 2 0 #0d000000`) and, under `:focus`, a border brush `#a1a1a1` and a 3 px ring shadow (shadcn/ui's `shadow-xs` and focus ring in Aioros.Cosmo)
- **When** the page is rendered
- **Then** the field's style carries the rest shadow and its `:focus` rule the focus border and ring, as Avalonia draws them, instead of the border color alone.

### TEXT-35: Transparent field backgrounds are written

- **Given** a `TextBox` and a disabled multi-line one whose theme's background is transparent
- **When** the page is rendered
- **Then** the `<input>` and the `<textarea>` have `background:transparent`, so the browser's own field background (white, or a gray when disabled) never shows.

### TEXT-36: Field states are read with their transitions held

- **Given** a `TextBox` whose theme gives `PART_BorderElement` transitions on its border brush and box shadow
- **When** the page is rendered
- **Then** its rest and `:focus` looks are read with the transitions held (the rest border `#e5e5e5`, the focus border `#a1a1a1`, not the rest border in both) and the transitions are written as the field's CSS `transition` (`border-color 150ms linear 0ms,box-shadow 150ms linear 0ms`).

### TEXT-37: Invalid fields are marked and described

- **Given** a `TextBox` bound to a member of an `INotifyDataErrorInfo` model, of a theme whose `:error` style sets the border `#e7000b` and whose `:error:focus` style a red ring
- **When** the page is planned and rendered for a valid and for an invalid model
- **Then** the plan is reusable and writes what Avalonia renders for both; while the model reports errors the field carries `aria-invalid="true"` and `aria-describedby` naming the element that holds its messages (`e{id}`, which wraps them with `display:contents`), and the field's `[aria-invalid="true"]` and `:focus[aria-invalid="true"]` rules carry the theme's error looks, instead of Fluent's error border forced over the theme's.

### TEXT-38: Validation messages take their themes look

- **Given** a `TextBox` whose `DataValidationErrors` theme places its messages 12 px below the field in a 14 px text on a 20 px line in `#e7000b` (Aioros.Cosmo's), and a Fluent `TextBox`
- **When** the page is rendered
- **Then** the message class carries that font, color, line height and `margin:12px 0px 0px 0px`, read from the error template Avalonia realizes for a message, and Fluent's carries its 14 px text and 4 px margin, instead of a fixed 12 px text with a 4 px margin; in a vertical stack with a 12 px spacing the margin is the same 12 px, since the message is written inside the field's item (TEXT-46), where the stack's spacing does not reach, as Avalonia lays it inside the field.

### TEXT-46: Messages share their fields grid cell

- **Given** a `Grid` of `Auto`, 12 px, `Auto`, 12 px, `Auto` and `*` rows holding a label, a `TextBox` whose `DataValidationErrors` theme places its message 12 px below the field (an error set in XAML, and in a second page one its model reports through `INotifyDataErrorInfo`) and a text after it
- **When** the page is rendered
- **Then** the field and its messages are one item (as they are in every parent: a stack, a dock, a wrap panel) that takes the field's cell, alignment, margin and size, the field above its messages, so the message stands 12 px below the field and the cell's height includes it, and in Chromium every text (the label, the message, the text after) stands where Avalonia places it on the desktop instead of the message being placed by the browser in another row; the model's messages are planned inside the same item.

*Precedent: Avalonia's `DataValidationErrors`, a template around the field that docks its messages below it, so the messages are part of the control's box wherever it is placed.*

### TEXT-47: A fields margin and width hold its messages

- **Given** a vertical `StackPanel` with a 12 px spacing holding a label, a `TextBox` with an error whose `Margin` is `24,8,16,20`, `Width` 240 and `HorizontalAlignment` `Left`, and a text after it
- **When** the page is rendered
- **Then** the item that holds the field and its message carries the margin, the width and the alignment, and the field and its message only fill it, so in Chromium the message is indented with the field and as wide as it (240 px, where it wraps) and the text after stands 20 px below the message and the stack's spacing, every text where Avalonia places it on the desktop; the field itself carries no margin and no width.

*Precedent: Avalonia's `Layoutable`, whose margin, size and alignment apply to the control's whole box, its `DataValidationErrors` messages included.*

### TEXT-48: A fields height holds its messages

- **Given** a vertical `StackPanel` holding a `TextBox` with an error and a `MinHeight` of 120, one with a `Height` of 110, and a text after each
- **When** the page is rendered
- **Then** the item that holds each field and its message carries the minimum height or the height and the field grows to fill what its message leaves, as `DataValidationErrors` docks the messages at the bottom and fills the rest with the field, so in Chromium the messages and the texts after stand where Avalonia places them on the desktop; the field's own minimum height is the largest one of the shown parts its template draws the field with inside `DataValidationErrors` (Fluent's `PART_BorderElement`, Aioros.Cosmo's panel), not the control's.

*Precedent: Avalonia's `DockPanel` (`LastChildFill`) inside `DataValidationErrors`, and `Layoutable`'s `MinHeight` and `Height` on the whole control; CSS flexbox's `flex-grow` for the fill.*

### TEXT-49: A fields effects hold its messages

- **Given** a vertical `StackPanel` holding a `TextBox` with an error whose `Opacity` is 0.5, `RenderTransform` a translation of 10 px right and 20 px down and `ZIndex` 3, a text after it, and in a second page a `TextBox` validated through `INotifyDataErrorInfo` whose `Opacity` is bound to the model (0.5)
- **When** the page is rendered
- **Then** the item that holds each field and its message carries the opacity (bound ones in its inline style, followed by the plan), the transform and the z-index, and the field's own element carries none of them, so in Chromium the message is faded and moved with the field (its effective opacity 0.5, not 0.25) and every text stands where Avalonia places it on the desktop.

*Precedent: Avalonia's `Visual`, whose `Opacity`, `RenderTransform`, `ZIndex` and clip apply to the control's whole rendering, the `DataValidationErrors` messages of its template included.*

### TEXT-50: Other fields hold their messages

- **Given** a vertical `StackPanel` holding a `ComboBox` with an error and a `MinHeight` of 120, a `NumericUpDown` with an error and a `Height` of 110 and a `CalendarDatePicker` with an error, each followed by a text
- **When** the page is rendered
- **Then** each control's item carries its minimum height or height, its own element fills what the message leaves and takes as its own minimum height the largest one of the shown parts its template draws the field with inside `DataValidationErrors` (the `NumericUpDown`'s and the `CalendarDatePicker`'s inner `TextBox`; none for the `ComboBox`), never the control's 120 px, so in Chromium the messages and the texts after stand where Avalonia places them on the desktop.

*Precedent: Fluent's templates for these controls, read in `Avalonia.Themes.Fluent` 12.1.3, each a `DataValidationErrors` around the control's parts, where any part with a minimum height holds the field at least that tall (TEXT-48 for the `TextBox`).*

### TEXT-51: Messages show only where the template places them

- **Given** a vertical `StackPanel` holding a `Button` with an error (`DataValidationErrors.Error`) and a `Height` of 36, followed by a text, and in a second page a `TextBox` whose theme's template holds no `DataValidationErrors` (a border around its presenter), bound to an `INotifyDataErrorInfo` model reporting an error for it
- **When** the page is rendered
- **Then** neither control is wrapped and neither writes a message or an `aria-describedby`, since their templates (Fluent's and Aioros.Cosmo's button, the bare text box theme) hold no `DataValidationErrors` and Avalonia shows none: the button is 36 px tall in Chromium as on the desktop, the text after stands where Avalonia places it, and the text box, planned on the model, carries `aria-invalid="true"` while the model reports the error; a control whose template holds a `DataValidationErrors` shows its messages as before (TEXT-46).

*Precedent: Avalonia's `DataValidationErrors`, which shows a control's errors only where its template places one (Fluent's `TextBox`, `NumericUpDown`, `CalendarDatePicker`, `ComboBox`, `DatePicker`, `TimePicker` and `Slider`, read in `Avalonia.Themes.Fluent` 12.1.3.*

## Library list

### TEXT-53: Code slots are marked for the script with their active look

- **Given** a library's one-time-code control (Aioros.Cosmo's `c:InputOtp`) whose template's slot list carries `a:Web.Slots` naming its text box and whose slot box carries `a:Web.SlotActive="active"`, a style giving its `.otp-box.active` a `#a1a1a1` border and a 3 px ring
- **When** the page is rendered and planned for an empty code
- **Then** the list carries `data-aioros-slots`, the text box `data-aioros-slots-input`, each slot box `data-aioros-slot-box` and the class of a rule under `[data-aioros-active]` with the active look (the border color and the ring), each slot's bound character text `data-aioros-slot-text`, and the plan writes what Avalonia renders.

*Precedent: input-otp (shadcn/ui's), whose slots show the hidden input's characters and ring the caret's slot while it has the focus; WAI-ARIA's one input for the code.*
