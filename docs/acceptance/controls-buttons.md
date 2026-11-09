# Controls: buttons

Buttons, toggles and links (`src/Adapters/Aioros.Avalonia/Html/Families/ButtonsFamily.cs`).

67 criteria. Each has an identifier and exactly one test.

## Button controls

### BUTTONS-01: Buttons submit their command

- **Given** a `Button`
- **When** it is rendered
- **Then** it is a `<button type="submit" name="_btn">` whose value is its field id and command parameter, with its content inside, and `disabled` when it is not enabled.

### BUTTONS-02: Hyperlink buttons are links

- **Given** a `HyperlinkButton`
- **When** it is rendered
- **Then** it is an `<a>` whose `href` is its `NavigateUri` and whose text is its content, with the Fluent look: its foreground color, underlined as `TextBlock.TextDecorations` says, and its padding.

### BUTTONS-03: Check boxes are checkboxes

- **Given** a `CheckBox`
- **When** it is rendered
- **Then** it is a checkbox `<input>` inside a `<label>` with its content, `checked` when it is checked
- **When** a post omits it
- **Then** it becomes unchecked.

### BUTTONS-04: Popover buttons are not fields

- **Given** a `Button` with `a:Web.Toggles` or a flyout
- **When** it is rendered
- **Then** it is a `type="button"` that opens the target popover, is not a field, and a `MenuFlyout` is written as a popover menu of links.

### BUTTONS-05: Accent buttons have the accent look

- **Given** a `Button` with `Classes="accent"`
- **When** it is rendered
- **Then** its style carries the Fluent accent look: the `AccentButtonBackground` fill and the `AccentButtonForeground` text, and its hover state the `AccentButtonBackgroundPointerOver` fill.

### BUTTONS-06: Repeat buttons submit their command once

- **Given** a `RepeatButton` with a command
- **When** it is rendered
- **Then** it is a `<button type="submit" name="_btn">` like a `Button`
- **When** a post presses it
- **Then** its command runs once.

### BUTTONS-07: Toggle buttons are hidden checkboxes in labels

- **Given** a `ToggleButton`
- **When** it is rendered
- **Then** it is a `<label>` with the Fluent toggle look holding a visually hidden checkbox `<input>` named by its field, `checked` when it is checked, and the Fluent checked look applies while the input is checked
- **When** a post omits it
- **Then** it becomes unchecked, and when a post sends it, then it becomes checked.

### BUTTONS-08: Radio buttons in a panel share one name

- **Given** `RadioButton`s without a `GroupName` in one panel
- **When** they are rendered
- **Then** each is a radio `<input>` in a `<label>` with its content, all sharing the field name of the first one, valued by their position in the group and `checked` when checked
- **When** a post sends a position
- **Then** that radio becomes checked and the others unchecked.

### BUTTONS-09: Radio button groups span panels

- **Given** `RadioButton`s with a `GroupName` spread over several panels
- **When** they are rendered
- **Then** the radios of one group share one field name across panels and a different group gets its own name
- **When** a post omits a group
- **Then** its radios keep their state.

### BUTTONS-10: Toggle switches are switch checkboxes

- **Given** a `ToggleSwitch`
- **When** it is rendered
- **Then** it is a `<label>` with its header content above a checkbox `<input role="switch">` drawn as the Fluent switch (track and knob, off and on fills), followed by its `OffContent` and `OnContent` of which only the one for the current state shows, `checked` when it is checked
- **When** a post omits it
- **Then** it becomes unchecked.

### BUTTONS-11: Drop down buttons open their flyout

- **Given** a `DropDownButton` with a flyout
- **When** it is rendered
- **Then** it is a `type="button"` with the Fluent button look, its content and a chevron, that opens its flyout as an anchored popover (a `MenuFlyout` as a menu of links, a `Flyout` as a panel with its content), and it is not a field.

### BUTTONS-12: Split buttons submit and open their flyout

- **Given** a `SplitButton` with a command and a flyout
- **When** it is rendered
- **Then**
  - It is a group of a primary `<button type="submit" name="_btn">` with its content that runs its command on post, and a secondary `type="button"` with a chevron that opens its flyout as an anchored popover
  - `disabled` when it is not enabled.

### BUTTONS-13: Toggle split buttons toggle and open their flyout

- **Given** a `ToggleSplitButton`
- **When** it is rendered
- **Then** its primary part is a `<label>` holding a visually hidden checkbox `<input>` named by its field, `checked` when it is checked, with the Fluent checked look while checked, and its secondary part opens its flyout
- **When** a post omits it
- **Then** it becomes unchecked.

### BUTTONS-14: Button spinners post their direction

- **Given** a `ButtonSpinner`
- **When** it is rendered
- **Then** it is a box with the Fluent text control look holding its content and an increase and a decrease `<button type="submit">`, each named by a field of its own, with the values `increase` and `decrease`, on the right or on the left after `ButtonSpinnerLocation`, `disabled` when spinning is not allowed in that direction and absent when `ShowButtonSpinner` is false
- **When** a post sends a direction
- **Then** its `Spin` event is raised with that direction, and when a post omits it, then no `Spin` event is raised.

### BUTTONS-15: Three state check boxes cycle through three radios

- **Given** a `CheckBox` with `IsThreeState`
- **When** it is rendered
- **Then** it is a group of three labels, each holding a visually hidden radio `<input>` for the next state in the cycle unchecked, checked, indeterminate, sharing its field name with the values `true`, `null` and `false`, the one for its current state `checked`, and only the label showing the current state (unchecked, checked or indeterminate glyph) is displayed
- **When** a post sends `null`
- **Then** it becomes indeterminate.

### BUTTONS-16: Buttons with bound text content are planned

- **Given** a `Button` whose `Content` is bound to text (in a page or an item template)
- **When** the page is compiled
- **Then** the plan is reusable and each render writes that model's text, encoded, inside the `<button>`; a `Button` whose `Content` is bound to a control still cannot be planned.

### BUTTONS-17: Buttons keep their own padding

- **Given** two `Button`s of the same type, classes and theme, one with its own `Padding` (`4`) and `Background` (`#ff0000`) and the other with others (`20,10` and `#0000ff`)
- **When** the page is compiled
- **Then** each `<button>` has the padding and the background Avalonia gives that button (`4px 4px 4px 4px` and `#ff0000`; `10px 20px 10px 20px` and `#0000ff`), whichever is written first: the theme look shared by the controls of one type and classes never carries one control's own values to another.

### BUTTONS-18: Check boxes have fluents box and height

- **Given** a Fluent `CheckBox` and one with `MinHeight="40"`
- **When** they are rendered
- **Then** each `<label>` is at least as tall as its `MinHeight` (Fluent's 32 px, and 40 px) with its content centered, and its checkbox `<input>` is 20 px square as Fluent's box, so the content starts 28 px from the left edge (the box and the 8 px gap) as in Avalonia's layout, and within half a pixel of the height where Avalonia lays it out (8 and 12 px down, the centered offset rounded to whole pixels), as a `RadioButton` does.

### BUTTONS-19: Disabled check boxes look disabled

- **Given** a disabled `CheckBox`, unchecked and checked
- **When** it is rendered
- **Then** its checkbox `<input>` is `disabled` and its `<label>` has Fluent's disabled foreground for its state (`CheckBoxForegroundUncheckedDisabled` or `CheckBoxForegroundCheckedDisabled`, `rgba(0,0,0,0.4)` for both in the light theme), as a disabled `RadioButton` does, instead of the enabled black.

### BUTTONS-20: Hyperlink buttons have their state looks

- **Given** a `HyperlinkButton` in the dark theme
- **When** it is rendered
- **Then** its `<a>` carries the looks Avalonia gives its content presenter with `:pointerover` and with `:pressed` as `:hover` and `:active` rules (Fluent's subtle fills, `rgba(255,255,255,0.059)` and `rgba(255,255,255,0.039)`, with the foreground where the state changes it), as a `Button` does, instead of keeping its normal look in those states.

### BUTTONS-21: Unbound content takes its typed template

- **Given** a `Button` whose content is an object that is neither a control nor bound (a `StreamGeometry`) and a `DataTemplate` for its type in scope
- **When** the page is compiled
- **Then** the plan is reusable and the button holds what the template builds (the `PathIcon`'s `<svg>` with the geometry's path) instead of the object's type name, as Avalonia's content presenter applies the template; a button with text content still holds the text.

### BUTTONS-42: Radio buttons checked through a converter

- **Given** radio buttons whose `IsChecked` is bound through a converter with an enum parameter (as AdvancedToDoList's priority radios use `EnumEqualsToIsCheckedConverter` with `{x:Static Priority.High}`)
- **When** the page is compiled
- **Then** the plan is reusable and checks the radio whose converter answers true, as Avalonia renders it for any model, also after the plans file; the radios' field is not described, so a post goes through the Avalonia engine and its converter.

### BUTTONS-77: Buttons take the theme a style sets

- **Given** buttons whose `ControlTheme` a page style sets (`<Style Selector="Button"><Setter Property="Theme" Value="{DynamicResource Flat}" /></Style>`, as Ursa's toast and notification pages set Semi's `SolidButton`)
- **When** the page is rendered
- **Then** each button is written with its theme's look as Avalonia shows it (the theme's light font weight, not the default theme's), and the plan is reusable and writes the same bytes, and a button whose own template sets a pseudo-class its style reads (a button without content marked `:bare` in `OnApplyTemplate`, which a style gives a minimum width, as Ursa's `IconButton` takes `:empty-content` in `ThemeToggleButton`) is written with that minimum width; every control is styled, and every button templated, before the page is written.

### BUTTONS-92: Bound link addresses are planned per item

- **Given** a `HyperlinkButton` whose `a:Web.Href` is `/` and, in the `ItemTemplate` of an `ItemsControl` bound to the model's crumbs, `HyperlinkButton`s and `Border`s whose content and `a:Web.Href` are bound to each crumb (as a breadcrumb of the model's pages)
- **When** the page is compiled and rendered for other models and the request path `/docs/b`
- **Then** the plan is reusable, writing what Avalonia's render writes for every model and after the plans file; each link is an `<a>` whose `href` is its `a:Web.Href` (encoded as an attribute, `&amp;` for `&`), followed by `data-nav` (a `HyperlinkButton` without `a:Web.Href` keeps its `NavigateUri`, BUTTONS-02); and the link whose address is the request path's (or its prefix up to a `/`) is marked `aria-current="page"` per item at request time (NAVIGATION-21), the others not.

### BUTTONS-96: Hot keys are written as shortcuts

- **Given** a `Button` with `HotKey="Ctrl+S"` and a command, a `Button` with `HotKey="Ctrl+K"` that opens a flyout, and a `Button` with `HotKey="Shift+F1"`
- **When** the page is rendered
- **Then** each `<button>` carries `data-aioros-key` naming its gesture by the browser's key codes (`ctrl+KeyS`, `ctrl+KeyK`, `shift+F1`) and `aria-keyshortcuts` in WAI-ARIA's form (`Control+S`, `Control+K`, `Shift+F1`), which the keyboard module runs (FOCUS-08); a button without a hot key carries neither; and the page plans, with no hidden key button for the window binding Avalonia's `HotKeyManager` registers for each hot key (its own command wrapper, the button already carrying the key).

*Precedent: Avalonia's `HotKeyManager.HotKey` (`Button.HotKey`), registered on the window; WAI-ARIA's `aria-keyshortcuts`.*

### BUTTONS-112: Links write their control content

- **Given** a `HyperlinkButton` whose content is a `TextBlock` (`Text="Read the docs"`, wrapping)
- **When** the page is rendered
- **Then** its `<a>` holds the text block written through its writer as a button's content is (the wrapping text block's `<div>` with its text), not the control's type name (`Avalonia.Controls.TextBlock`); a link of text content keeps its text.

### BUTTONS-116: Choice hot keys are written as shortcuts

- **Given** a `RadioButton` with `HotKey="B"`, a card `RadioButton` of BUTTONS-114 with `HotKey="C"` and a `CheckBox` with `HotKey="Ctrl+D"`
- **When** the page is rendered
- **Then** each `<input>` carries its key as a page shortcut (`data-aioros-key="KeyB"`, `"KeyC"`, `"ctrl+KeyD"`) and as WAI-ARIA's `aria-keyshortcuts`, as a button's hot key does (need 59's shortcuts click the input, which checks it).

### BUTTONS-121: A button waiting for an answer is marked

- **Given** a panel marked `a:Web.Answers` holding a radio button, a disabled button marked `a:Web.NeedsAnswer` and another button
- **When** the page is rendered
- **Then** the panel carries `data-aioros-answers` and the marked button `data-aioros-needs-answer`, which the framework's script follows (SSR-254), and the other button no mark.

*Precedent: shadcn/ui's `QuestionnaireNavigationState` example.*

### BUTTONS-122: A key scopes buttons are marked with their keys

- **Given** a panel marked `a:Web.KeyScope` holding a button with `a:Web.Keys` `Ctrl+Enter Right`, a button whose keys are `Nope Left` and a button with none
- **When** the page is rendered
- **Then** the panel carries `data-aioros-keyscope`, the first button `data-aioros-keys="ctrl+Enter ArrowRight"`, the second `data-aioros-keys="ArrowLeft"` (a key Avalonia's gestures do not name left out), the third no mark, and the page loads the keyboard module, which follows them (SSR-255).

*Precedent: shadcn/ui's questionnaire (`@shadcn/react` 0.3.1's root `onKeyDown`: Ctrl or Meta and Enter, the arrows, Enter on an answer), Avalonia's `KeyGesture.Parse`, WAI-ARIA's `aria-keyshortcuts`.*

### BUTTONS-123: Stacked lines in a button start at its left

- **Given** a button whose content stacks a short and a long line of text (a name above an e-mail address, as the user menu at the foot of Aioros.Cosmo's sidebar, a button that opens a flyout)
- **When** it is rendered in the browser
- **Then** both lines start at the same left edge as Avalonia lays them out on the desktop, where a `TextBlock`'s alignment is its own (`Left` by default) and never inherited from the button: the browser's style for a `<button>` centers its text (`text-align:center`), so a `<button>` Aioros writes starts it at its start edge (`text-align:start`), a button that opens a flyout too (it was written without it), and only a `TextBlock` whose `TextAlignment` says otherwise centers or ends its lines.

### BUTTONS-124: Buttons with an address are links

- **Given** a button with no command and `a:Web.Href` (a sidebar's menu button, `/docs`, or one bound to its model's address)
- **When** the page is written
- **Then**
  - It is a link (`<a href="/docs">`) with the button's look and content, as a `HyperlinkButton` is, so following it navigates; a button with a command and an `a:Web.Href` stays a button that posts its command.
  - Its content is centered as the button centers it: a `<button>` centers its content itself, the link by `display:inline-flex` with its items and content centered.

### BUTTONS-125: Radio groups are answered from the plan as the engine answers them

- **Given** a radio group of three `RadioButton`s each bound two way to a `bool` of the model, and one of three bound two way to one enum through a converter with a parameter per radio (the usual Avalonia pattern)
- **When** the page is compiled for the web
- **Then** its plan is reusable and describes the group's field, so the page's posts are answered from the plan
- **When** a post picks the third radio through the plan
- **Then** the models are what the engine leaves for the same post: the three `bool`s `false`, `false`, `true`, and the enum `Large` (the converter's `ConvertBack` of the third radio's parameter), and the answer writes the third radio checked.

### BUTTONS-126: A link places its content as its alignment says

- **Given** a `HyperlinkButton` stretched across a 300 px panel with `HorizontalContentAlignment="Center"`, and one with `HorizontalContentAlignment="Right"`
- **When** the page is laid out in a browser
- **Then** each text sits where Avalonia places it, the first centered and the second at the right: a link does not center its content by itself, as a `<button>` does.

## File picker

### BUTTONS-63: File pickers are file inputs in a label

- **Given** an `a:FilePicker` whose `File` is bound two-way and whose `FileTypes` hold Avalonia's `FilePickerFileTypes.ImageAll` and a type with the pattern `*.pdf` and the MIME type `application/pdf`
- **When** it is rendered
- **Then** it is a `<label>` with the button's look holding its content and a visually hidden `<input type="file" name="f{id}">` without `multiple`, whose `accept` lists each pattern as an extension (`*.png` as `.png`) and each MIME type once, `disabled` when the picker is not enabled; the page is marked as taking uploads, and its plan is reusable while its field is not described to plans, so its posts go through the engine.

### BUTTONS-64: File pickers of a list take several files

- **Given** an `a:FilePicker` whose `Files` is bound to a list and whose `FileTypes` include `*.*`
- **When** it is rendered
- **Then** its file input carries `multiple` and no `accept`.

### BUTTONS-65: A posted file is a storage file

- **Given** an `a:FilePicker` whose `File` is bound two-way
- **When** a post carries a file for its field
- **Then** the bound property is Avalonia's own `IStorageFile` for the upload's temporary file (from the window's `StorageProvider`, since Avalonia's storage interfaces cannot be implemented outside it), named as the uploaded file, whose `OpenReadAsync` reads the posted bytes and whose `GetBasicPropertiesAsync` gives their size, and the pressed button's command reads it
- **When** a post carries no file for the field
- **Then** the property keeps its value.

### BUTTONS-66: Several posted files are a list

- **Given** an `a:FilePicker` whose `Files` is bound two-way to an `IReadOnlyList<IStorageFile>`
- **When** a post carries two files for its field
- **Then** the list holds both, in the order posted, each reading its own bytes.

### BUTTONS-67: Clicking opens the storage provider

- **Given** an `a:FilePicker` in a window
- **When** it is clicked
- **Then** it opens its top level's `StorageProvider` file picker (`OpenFilePickerAsync`) titled with its text content, with `AllowMultiple` only when `Files` is bound and its `FileTypes` as the filter, and the chosen file becomes `File` (all chosen files `Files`)
- **When** the picker is cancelled
- **Then** `File` keeps its value.

### BUTTONS-117: A themed file picker is written through its template

- **Given** an `a:FilePicker` with a library's control theme (Aioros.Cosmo's file input: a 36 px high picker whose `PART_Root` `Border` takes its 1 px `#e5e5e5` border, 6 px radius and `12,4` padding, holding a left-aligned row of a medium "Choose File" text and a `PART_FileName` text showing the chosen file's name, or "No file chosen")
- **When** the page is rendered
- **Then** it is still a `<label>` holding its hidden file input, its class carries the root's border, radius and padding, it holds the template's two texts, the name text marked `data-aioros-file-name`, the first text where Avalonia places it and the second its 8 px after on the same line, as on the desktop (SSR-251 writes the chosen name in it).

*Precedent: shadcn/ui's `<Input type="file">` (the `file:` button styles beside the chosen name), Aioros's library buttons written through their template (BUTTONS-91).*

### BUTTONS-118: A themed file picker names its choice on the desktop

- **Given** an `a:FilePicker` themed with a `PART_FileName` text reading "No file chosen" on the desktop
- **When** the user picks `photo.png`
- **Then** the text reads `photo.png`; when a picker of several picks `a.pdf` and `b.pdf`, it reads `a.pdf, b.pdf`; and when the model clears the file, it reads "No file chosen" again, as Chromium's file input names its choice (SSR-251 does the same on the web).

### BUTTONS-119: An implicit file picker theme is taken by its style key

- **Given** resources holding a `ControlTheme` keyed by `a:FilePicker`'s type (a library's implicit file input theme)
- **When** an `a:FilePicker` with no `Theme` of its own is shown
- **Then** it takes that theme (its template's `PART_FileName` is found), and an `a:FilePicker` under resources without one takes the `Button` theme, as Avalonia picks a control's implicit theme by its style key.

### BUTTONS-127: A themed empty text follows its binding

- **Given** an `a:FilePicker` whose theme's `PART_FileName` binds its text with a `TemplateBinding` (a translated empty text)
- **When** the bound value changes from `A` to `B` while no file is chosen
- **Then** the text reads `B`; when the user picks `photo.png`, it reads `photo.png`; when the file is cleared and the value has changed to `C` meanwhile, it reads `C`, the theme's binding being in charge again as it was.

## Button theme

### BUTTONS-78: Focus visible looks of the theme are written

- **Given** a `Button` whose control theme sets, under `^:focus-visible /template/ ContentPresenter#PART_ContentPresenter`, the presenter's `BorderBrush` (`#999999`) and `BoxShadow` (`0 0 0 3 #80999999`), as shadcn/ui's focus ring in Aioros.Cosmo
- **When** the page is rendered
- **Then** the `<button>` carries a `:focus-visible` rule with `border-color:#999999` and `box-shadow:0px 0px 0px 3px rgba(153,153,153,0.502)`, as Avalonia gives the keyboard-focused button, and since the theme sets `FocusAdorner` to null the browser's own focus outline is off (`outline:none`), as Avalonia draws no focus adorner; a Fluent button, whose theme has no `:focus-visible` style and whose focus adorner Fluent draws, gets no `:focus-visible` rule and keeps the browser's outline.

### BUTTONS-79: Template part shadows are written

- **Given** a `Button` whose control theme gives its content presenter a `BoxShadow` of two shadows, one inset with a spread and colors with alpha (`0 1 2 0 #0d000000, inset 0 0 0 1 #33ff0000`)
- **When** the page is rendered
- **Then** the button's style carries `box-shadow:0px 1px 2px 0px rgba(0,0,0,0.051),inset 0px 0px 0px 1px rgba(255,0,0,0.2)`, and a `:pointerover` style that changes the shadow writes the new shadow in the `:hover` rule.

### BUTTONS-80: Pressed looks compose with pointer over

- **Given** a `Button` whose control theme has a `^:pointerover` style (background `#333333`) and no `:pressed` style
- **When** the page is rendered
- **Then** its `:hover` rule sets `#333333` and no `:active` rule brings back the rest background, since Avalonia's pressed button is also pointed; a theme with a `^:pressed` style (background `#555555`) gets an `:active` rule with it after the `:hover` rule, and a `^:pointerover:pressed` style that differs from both (background `#777777`) gets a `:hover:active` rule.

### BUTTONS-81: Disabled buttons have no state rules

- **Given** an enabled and a disabled `Button` of a theme with `:pointerover` and `:pressed` styles
- **When** the page is rendered
- **Then** the enabled button's state rules apply only while it is not `:disabled` (`:not(:disabled):hover`, `:not(:disabled):active`) and the disabled button has no state rules, as Avalonia never points at or presses a disabled control.

### BUTTONS-82: Hyperlink buttons follow their theme

- **Given** a `HyperlinkButton` whose control theme is based on a button theme with a border brush (`#e5e5e5`, 1 px) and no text decorations
- **When** the page is rendered
- **Then** its `<a>` has `border-color:#e5e5e5` and no underline at rest or on hover, as its theme says; a Fluent `HyperlinkButton` stays underlined as its theme's `TextDecorations` say.

### BUTTONS-83: Pointer over styles set text decorations

- **Given** a `Button` whose control theme sets `TextBlock.TextDecorations` to `Underline` in a `^:pointerover` style (shadcn/ui's `link` variant)
- **When** the page is rendered
- **Then** the `<button>` has `text-decoration:none` at rest and its `:hover` rule sets `text-decoration:underline`.

### BUTTONS-84: Content line height of the template is written

- **Given** a `Button` whose template sets `TextBlock.LineHeight="20"` on its content presenter
- **When** the page is rendered
- **Then** the `<button>` has `line-height:20px`, so its text content is laid on Avalonia's line; a Fluent button keeps the line height of its font.

### BUTTONS-85: Bound object content takes its template

- **Given** a `Button` whose `Content` is bound to a model object, one with an explicit `ContentTemplate` and one with a typed `DataTemplate` in scope (each a `TextBlock` bound to the object's `Name`)
- **When** the page is compiled
- **Then** the plan is reusable and each render writes inside each `<button>` what the template builds for that model's object (its name), as Avalonia renders it, instead of the object's `ToString`.

### BUTTONS-91: Template parts around the content are written

- **Given** a `Button` whose control theme's template is a `PART_Root` `Border` (transparent, its padding `12,0` from the button's, `#f5f5f5` under `:pointerover`, a `BoxShadow` under `:focus-visible`) holding a row of a 16 px chevron `Path` in a `Viewbox` and the content presenter, as Aioros.Cosmo's `c:PaginationPrevious`, and a button of the same template that opens a `Flyout`, as `c:NavigationMenuTrigger`
- **When** the page is rendered
- **Then** each is still a native `<button>` (a submit with its `_btn` value, the flyout's a `popovertarget` toggle) whose class has the root part's box (`padding:0 12px`) and whose state rules give it the root's `:hover` background and `:focus-visible` shadow, and inside it the template's parts are written in order: the row with the chevron's `<svg>` and the text; a `Content` bound to the model plans reusable, writing each model's text after the chevron as Avalonia's render does; a button whose template's root is the content presenter (Fluent's) is written as before, its content alone.

### BUTTONS-95: Zero padding of the theme is written

- **Given** a `Button` whose control theme sets `Padding="0"` and a transparent background that its template's `PART_ContentPresenter` takes (Aioros.Cosmo's breadcrumb trigger), and a `Button` of a theme with `Padding="16,0"`
- **When** the page is rendered
- **Then** the first `<button>`'s style has `padding:0` and no background of its own (the document's base style gives every button `background:none`), so neither the browser's own `padding:1px 6px` nor its button face shows, and the second has `padding:0px 16px 0px 16px`.

### BUTTONS-99: Content alignment of the template is written

- **Given** a `Button` whose template is its `PART_ContentPresenter` with `HorizontalContentAlignment="Stretch"` and the presenter's default vertical alignment (Aioros.Cosmo's navigation menu link, a title and a description in a `StackPanel`), a `Button` of that theme with `HorizontalContentAlignment="Right"` and `VerticalContentAlignment="Bottom"`, a `Button` of that theme centering its content both ways, and a Fluent `Button` (whose presenter stretches its content, as Avalonia's Fluent theme leaves it)
- **When** the page is rendered
- **Then** the first button lays its content out from its start as Avalonia's presenter does (`display:grid;justify-items:stretch;align-items:stretch;text-align:start`, so the browser's centering of a button's content does not apply), the second `justify-items:end;align-items:end;text-align:end`, the centered one none of them (the browser's own alignment), and the Fluent button `justify-items:stretch;align-items:stretch;text-align:start`.

### BUTTONS-100: Open flyout looks of the theme are written

- **Given** a `Button` with a `Flyout` whose control theme gives it a background and a foreground under `:flyout-open`, another background under `:flyout-open:pointerover`, and turns a template part (a chevron `Viewbox`) by 180° under `:flyout-open` (Aioros.Cosmo's navigation menu trigger)
- **When** the page is rendered
- **Then** the button's look under `:flyout-open` is a rule under `:has(+:popover-open)` (its popover is open; Radix's `data-state=open`), the combined state a rule under `:hover:has(+:popover-open)`, and the chevron carries a class turning it by 180° while its button's popover is open (`[popovertarget]:has(+:popover-open) .name{transform:…}`).

### BUTTONS-111: Buttons of parts lay their content out as their template does

- **Given** a `Button` whose template's root is a named border around its `PART_ContentPresenter` aligned left and centered down, one of that theme centering its content both ways, and a `Button` whose template's root is a panel of parts (a named border around a dock panel holding the presenter, as Aioros.Cosmo's sidebar header, footer and sub-menu buttons)
- **When** the page is rendered
- **Then** the first lays its content out as its presenter aligns it (`display:grid;justify-items:start;align-items:center;text-align:start`) instead of the browser centering it, as BUTTONS-99 does for a template that is its presenter; the centered one keeps the browser's own alignment; and the panel's button lays its parts out over its whole box with its texts from their start (`display:grid;justify-items:stretch;align-items:stretch;text-align:start`), as Avalonia stretches a template's root.

### BUTTONS-113: Copying buttons copy in the browser

- **Given** a button marked `a:Web.Copies` with a text holding a quote and an angle bracket, whose template overlays a copy icon and a check icon at opacity 0 and whose theme swaps their opacities under `:copied` (Aioros.Cosmo's `c:CodeBlockCopyButton`), a button whose `a:Web.Copies` is bound to the model's `Command`, and a plain button after them
- **When** the page is planned
- **Then**
  - The plan is reusable and writes what Avalonia renders; each copying button is a `<button type="button" data-aioros-copies="…">` holding its text escaped (the bound one the model's, for each model), not a field of the form (the plain button keeps `value="0|"`), and its class gives its template's parts their `:copied` looks under `[data-copied]` (the copy icon `opacity:0`, the check `opacity:1`).
  - The native targets ignore the hint: the button's own code copies.
  - Precedent: shadcn/ui's `CodeBlockCopyButton` (`navigator.clipboard.writeText`, then its check for 2000 ms).

### BUTTONS-120: A link themed with more than its content is written through its template

- **Given** a `HyperlinkButton` whose theme's template is a named `Border` holding an icon beside its `PART_ContentPresenter` (a library's link with an icon, Aioros.Cosmo's marker link), a `:pointerover` style on that border, a `:pressed` style setting the link's foreground, a style underlining its content presenter, and a plain `HyperlinkButton`
- **When** the page is rendered
- **Then** the themed link is one `<a href>` whose look is the border's (its background, its hover background in a `:hover` rule, and `text-decoration:none`, so the browser's own underline of a link does not show) and which holds the template's parts, the icon and then the content, its text underlined as the presenter gives it and taking its color from the link, so the link's pressed color (an `:active` rule) colors it as Avalonia's inherited foreground does; the plain link stays its content alone in `<a href>`.

*Precedent: Avalonia's `HyperlinkButton` (a `Button` whose template a theme replaces), Semi.Avalonia's and shadcn/ui's links with an icon (`MarkerLinkButton` with `asChild`), HTML's `<a>` holding flow content.*

## Toggle theme

### BUTTONS-86: Check boxes draw their themes mark

- **Given** a `CheckBox` whose control theme draws a 16 px `PART_Border` (border `#e5e5e5`, 1 px, radius 4, a `BoxShadow`) holding a stroked check `Path` shown under `:checked` with the border's background `#171717`, a `:focus-visible` shadow, a content presenter 12 px from the box with `TextBlock.LineHeight="14"` and `FocusAdorner` null (Aioros.Cosmo's), and a Fluent `CheckBox`
- **When** the page is rendered
- **Then** each checkbox `<input>` is drawn with `appearance:none` as its theme draws the mark: the themed one 16 px square with that border, radius, shadow and a transparent fill at rest, `#171717` and the check glyph as an SVG background from the template's own path (stroke `#fafafa`) while checked, the focus shadow while focused from the keyboard and no browser outline, its `<label>` laying the content 12 px from the box on a 14 px line; the Fluent one 20 px square with an 8 px gap, as Fluent's `NormalRectangle` and padding say.

### BUTTONS-87: Radio buttons draw their themes circle

- **Given** a `RadioButton` whose control theme draws a 16 px round `PART_Border` (border `#e5e5e5`) holding an 8 px `Ellipse` dot shown under `:checked`, with its content 12 px away
- **When** the page is rendered
- **Then** its radio `<input>` (with no class of its own) is drawn with `appearance:none` 16 px square with that border and an 8 px radius, the dot as an SVG background (fill `#171717`) while checked, and its `<label>` lays the content 12 px from the circle.

### BUTTONS-88: Toggle switches draw their themes track and knob

- **Given** a `ToggleSwitch` whose control theme draws a 32 by 18 px `OuterBorder` (`#e5e5e5`, `#171717` when checked) with a 16 px knob `Ellipse` in `PART_MovingKnobs` on a 14 px `PART_SwitchKnob` canvas 1 px from the left, its content to the right 8 px away, no on and off contents and a 150 ms knob transition
- **When** the page is rendered
- **Then** its `<input role="switch">` is drawn 32 by 18 px with the track's fill and radius, the knob as an SVG background at `1px 1px` (`15px 1px`, the canvas's width further, while checked) with `transition:background-position 150ms cubic-bezier(0.4,0,0.2,1) 0ms`, and the `<label>` holds the switch then its content in a row 8 px apart, with no on and off spans.

### BUTTONS-89: Toggle button states follow the theme per class

- **Given** two `ToggleButton`s of one control theme, one checked and one with the `outline` class whose `:pointerover` style sets another foreground (`#262626` where the default's is `#737373`)
- **When** the page is rendered
- **Then** each `<label>` has its theme's unchecked look at rest (transparent, `#0a0a0a`) whatever its current state, the `:checked` look under `:has(>input:checked)`, and its own class's `:hover` look, as Avalonia's styles resolve them per control, instead of one set of Fluent keys for every toggle.

### BUTTONS-90: Three state check boxes draw their themes mark

- **Given** a three-state `CheckBox` of the theme of BUTTONS-86 whose `:indeterminate` style fills the box `#525252`
- **When** the page is rendered
- **Then** each of its three labels draws its `.box` as the theme's 16 px box with radius 4, the checked label (`c3`) with `#171717` and the indeterminate one (`i3`) with `#525252`, as the theme draws each state.

### BUTTONS-114: Card radio buttons hold their templates parts

- **Given** `RadioButton`s whose control theme (not Fluent's) is a card: a named root border (`PART_Card`, a 1 px `#e5e5e5` border, an 18 px radius, 16 by 12 px padding) around a grid of a 16 px round indicator holding an 8 px dot shown under `:checked`, a stack of the content presenter and a description text, and a 20 px shortcut border holding a text; the card filled `#f0f0f0` and bordered `#171717` under `:checked` (Aioros.Cosmo's questionnaire choice); and a Fluent `RadioButton` beside them
- **When** the page is rendered
- **Then** each themed radio is a `<label>` that takes the card's look (its border, radius and padding at rest, its fill and border `:has(input:checked)`, its fill under `:hover`) and holds the template's parts laid out as the grid lays them: the indicator written as the radio `<input>` itself, drawn as the theme draws the indicator (`appearance:none`, 16 px, the dot as an SVG background while checked), the content, the description and the shortcut's text; the radios share one field as BUTTONS-08 says; a disabled checked card, whose state cannot change, is written in its checked look; a radio whose theme's root is a named border with no frame around only its mark and its content (Semi.Avalonia's) and the Fluent radio are written as before (BUTTONS-87, BUTTONS-08).

### BUTTONS-115: Card check boxes hold their templates parts

- **Given** a `CheckBox` whose control theme (not Fluent's) is the card of BUTTONS-114 with a 16 px square indicator holding a check `Path` shown under `:checked`
- **When** the page is rendered
- **Then** it is a `<label>` taking the card's look with its checked look `:has(input:checked)`, holding its checkbox `<input>` drawn as the indicator (the check as an SVG background while checked) among the template's other parts, and a post that omits it unchecks it as BUTTONS-03 says.

## Key binding

### BUTTONS-97: Key bindings are written as hidden command buttons

- **Given** a page whose root `UserControl` has a `KeyBinding` for Ctrl+S bound to a method command (`SayHello`, enabled by `CanSayHello`) and a `StackPanel` with a `KeyBinding` for Ctrl+Shift+A bound to `ArchiveAsync` with `CommandParameter="30"`
- **When** the page is rendered
- **Then** each binding is a hidden `<button type="submit" name="_btn">` numbered with the page's buttons (`0|`, `1|30`) and carrying `data-aioros-key` and `aria-keyshortcuts` as a hot key does (BUTTONS-96): the root's (and its window's) first in the page and document-level, the panel's right before the panel's element and marked `data-aioros-scope`; the Ctrl+S button is `disabled` while `CanSayHello` is false and enabled once it is true, and the plan writes what Avalonia renders in both states; a `KeyBinding` whose command is not bound to the model (set in code) leaves the page to the Avalonia engine with the reason `KeyBinding.Command`.

*Precedent: Avalonia's `KeyboardDevice`, which tries the `KeyBindings` of the focused element and then of each ancestor up to the top level, and `KeyBinding.TryHandle`, which runs the command only when it can execute.*

### BUTTONS-98: Key bindings run their commands when posted

- **Given** the page of BUTTONS-97
- **When** a post names the panel's key binding with `1|30`
- **Then** `ArchiveAsync(30)` runs, through the plan and through the Avalonia engine
- **When** a post names the root's binding while `CanSayHello` is false
- **Then** nothing runs
- **When** a post names a key binding with a parameter it was not written with (`1|31`)
- **Then** the post is a conflict (`409`) and nothing runs.

*Precedent: Avalonia's `KeyBinding.TryHandle` (`CanExecute`, then `Execute(CommandParameter)`); "Buttons verified after binding".*

## Shell

### BUTTONS-110: Buttons whose parts have no border reset the browsers

- **Given** a templated button whose template's root is a panel holding a 2 px line (no border and no content presenter at its root: Aioros.Cosmo's sidebar rail, a 16 px strip), and a button whose template's root is a named border with a 1 px border
- **When** they are rendered
- **Then** the first button's element resets the browser's button border and padding (`border-width:0;padding:0;`), so only its parts show, and the second, whose border's look is its own, is not reset.
