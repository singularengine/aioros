# Parity

Parity means the same XAML looks and behaves the same when rendered natively and on the web. This page lists every XAML construct and how close the web is to the native render. The unit is the construct, not the app: a control, one of its states, a layout property or a selector.

The list is not chosen by hand. `tests/Aioros.ArchitectureTests/Parity/ParityInventory.cs` reads it from Avalonia's own assemblies, and the build fails if a construct has no row here, or if a row claims something its evidence does not support.

## Contents

- [Statuses](#statuses)
- [How it is measured](#how-it-is-measured)
- [Known differences](#known-differences)
- [Summary](#summary)
- [The inventory](#controls-control)

## Statuses

| Status | Meaning |
| --- | --- |
| `equal` | A criterion shows the web carries the native value one to one, or the bench measured it equal |
| `different` | Measured and different, with the reason |
| `unmeasured` | Written on the web, but not yet measured against the native render |
| `not supported` | Not written on the web yet |
| `n/a` | Has no meaning on the web, such as window chrome |

Most styling is resolved by Avalonia itself. Each page is laid out with its styles, themes and resources applied, and the web receives the computed result. So a selector or a style works for what the page contains, whichever form it was written in.

## How it is measured

`bench/compat/run.sh <app> [--check | --update]` starts a sample and renders every page natively and in Chromium, in both themes and four interaction states: normal, pointer over, pressed and focus. It then compares four things:

| Metric | What is compared | Equal when |
| --- | --- | --- |
| Texts | The native render's visible texts, found in the browser | The text is shown |
| Layout | The box of each matched text | Every edge is within 1 px |
| Colors | Each matched text's background and ink | Background within 3 per channel, ink within 48 |
| Pixels | Content pixels | Glyphs within 40 in luminance, the rest within 8 in color |

A baseline per app is kept in `bench/compat/baselines`. With `--check`, a metric that falls below its baseline fails, and CI runs both the Gallery and ControlCatalog this way.

The Control Gallery shows these numbers itself at `/parity`, with both renders side by side.

## Known differences

- **Glyph edges.** Skia and Chromium antialias text differently, so thin dark text can differ slightly in light themes.
- **Width rounding.** Avalonia rounds each control's width up to a whole pixel, while the browser keeps fractions. Items in a row can drift by up to 0.6 px each.
- **Shrink-wrapped wrapping.** A centered `WrapPanel` with a `MaxWidth` is as wide as its widest row natively, but CSS cannot shrink-wrap wrapped content.
- **Focus.** The browser draws an outline under `:focus-visible`, while Avalonia's focus adorner appears only with real keyboard focus.

## Summary

| Kind | Constructs | equal | different | unmeasured | not supported | n/a |
| --- | --- | --- | --- | --- | --- | --- |
| Controls | 134 | 14 | 3 | 92 | 0 | 25 |
| States | 72 | 0 | 4 | 31 | 16 | 21 |
| Panels and layout properties | 101 | 16 | 0 | 51 | 25 | 9 |
| Text | 36 | 3 | 0 | 20 | 13 | 0 |
| Selectors | 13 | 0 | 0 | 13 | 0 | 0 |
| Styles, themes and resources | 11 | 0 | 0 | 10 | 1 | 0 |
| Bindings | 14 | 8 | 0 | 6 | 0 | 0 |
| Templates | 10 | 1 | 0 | 7 | 1 | 1 |
| Transitions and animations | 62 | 0 | 0 | 22 | 40 | 0 |
| Markup extensions | 8 | 1 | 0 | 5 | 2 | 0 |
| Brushes, effects and transforms | 24 | 12 | 0 | 2 | 10 | 0 |
| **Total** | **485** | **55** | **7** | **259** | **108** | **56** |

## Controls (`control`)

Every public control of `Avalonia.Controls`, with its states checked by the bench. The web column of `docs/controls.md` says how each is written.

| Construct | Status | Reason | Criteria and evidence |
| --- | --- | --- | --- |
| `Avalonia.Controls.AutoCompleteBox` | unmeasured | `<input>` with pattern, `type=number` or a `<datalist>` | TEXT-14, TEXT-15 |
| `Avalonia.Controls.Border` | equal | Flex and CSS grid, borders (a thickness keeps its width without a brush), scrolling | LAYOUT-04, LAYOUT-21, LAYOUT-22, LAYOUT-27, bench:/border |
| `Avalonia.Controls.Button` | unmeasured | `<button type=submit>` running the command on post; content that is not bound takes its typed data template | BUTTONS-01, BUTTONS-04, BUTTONS-05, BUTTONS-21 |
| `Avalonia.Controls.ButtonSpinner` | unmeasured | Box with its content and increase and decrease submit buttons raising `Spin` on post | BUTTONS-14 |
| `Avalonia.Controls.Calendar` | unmeasured | Static month, year or decade table of the dates Avalonia computed | RANGES-06, RANGES-07 |
| `Avalonia.Controls.CalendarDatePicker` | unmeasured | `<input type=date>` bound to `SelectedDate` on post | RANGES-08, RANGES-09 |
| `Avalonia.Controls.Canvas` | equal | One `<svg>` at the canvas size without a view box: each child where the canvas arranges it, in `ZIndex` order, shapes as their rendered geometry, other controls in a `<foreignObject>`; the bench's vectors page matches 99.3% of content pixels (antialiased edges) | MEDIA-01, MEDIA-11, MEDIA-16, MEDIA-18, MEDIA-19, bench:/controls/vectors |
| `Avalonia.Controls.Carousel` | unmeasured | Horizontal scroll-snap slides; the selected slide is the initial scroll target | ITEMS-08 |
| `Avalonia.Controls.CarouselPage` | unmeasured | A horizontal scroll-snap strip of pages starting at the selected one | NAVIGATION-19 |
| `Avalonia.Controls.CheckBox` | equal | `<input type=checkbox>` (Fluent's 20 px box) in a `<label>` at least `MinHeight` tall, the disabled foreground when disabled; three-state as three cycling radios | BUTTONS-03, BUTTONS-15, BUTTONS-18, BUTTONS-19 |
| `Avalonia.Controls.ComboBox` | equal | `<select>` with `<option>`s keyed by index and the selected item matched as Avalonia matches it, as tall as Avalonia lays it out; a two-way selection posts when it changes | ITEMS-01, ITEMS-13, ITEMS-16, ITEMS-18 |
| `Avalonia.Controls.ComboBoxItem` | unmeasured | Part of `ComboBox` (`<option>`) | ITEMS-01 |
| `Avalonia.Controls.CommandBar` | unmeasured | `<menu>` toolbar; secondary commands in a "more" popover menu | NAVIGATION-10 |
| `Avalonia.Controls.CommandBarButton` | equal | Submitting `<button>` with icon (at its own size) and label | NAVIGATION-10, NAVIGATION-22 |
| `Avalonia.Controls.CommandBarSeparator` | unmeasured | `<li role="separator">` in the toolbar, `<hr>` in the overflow menu | NAVIGATION-10 |
| `Avalonia.Controls.CommandBarToggleButton` | unmeasured | `<label>` with a checkbox field | NAVIGATION-11 |
| `Avalonia.Controls.ContentControl` | unmeasured | Flex and CSS grid, borders, scrolling | LAYOUT-06 |
| `Avalonia.Controls.ContentPage` | equal | `<section>` with its top bar, content (inset by its theme template's margins) and bottom bar | NAVIGATION-15, NAVIGATION-23, bench:/border |
| `Avalonia.Controls.ContextMenu` | n/a | Opens on right click or long press, which no HTML or CSS mechanism can bind to a popover without script; the browser keeps its own context menu. Put the commands in a `MenuFlyout` on a button |  |
| `Avalonia.Controls.Control` | unmeasured | Its template; a self-drawn control is an inline SVG recorded from its `Render` (solid fills, strokes, glyph outlines, transforms, geometry clips, opacity), or a PNG of its Avalonia render otherwise | LAYOUT-20, LAYOUT-23, LAYOUT-25, LAYOUT-26 |
| `Avalonia.Controls.DataValidationErrors` | unmeasured | Error messages in `<div role="alert">` after the field, rendered after a post | NAVIGATION-13 |
| `Avalonia.Controls.DatePicker` | unmeasured | `<input type=date>` bound to `SelectedDate` on post | RANGES-10, RANGES-11 |
| `Avalonia.Controls.DatePickerPresenter` | n/a | Flyout of `DatePicker`; the browser's native date picker replaces it |  |
| `Avalonia.Controls.Decorator` | unmeasured | Flex and CSS grid, borders, scrolling | LAYOUT-06 |
| `Avalonia.Controls.DockPanel` | unmeasured | CSS grid with a track per docked child | LAYOUT-08 |
| `Avalonia.Controls.DrawerPage` | unmeasured | `<aside popover>` drawer with a toggle button; inline above `DrawerBreakpointLength` | NAVIGATION-18 |
| `Avalonia.Controls.DropDownButton` | unmeasured | `<button type=button>` with a chevron opening its flyout popover | BUTTONS-11 |
| `Avalonia.Controls.Embedding.EmbeddableControlRoot` | n/a | Native embedding |  |
| `Avalonia.Controls.Expander` | unmeasured | `<details>`/`<summary>` | NAVIGATION-02 |
| `Avalonia.Controls.ExperimentalAcrylicBorder` | unmeasured | Material colors with `backdrop-filter:blur()` | LAYOUT-18 |
| `Avalonia.Controls.FlyoutPresenter` | unmeasured | Anchored `<div popover class="fly">` with the presenter look | NAVIGATION-07 |
| `Avalonia.Controls.Grid` | unmeasured | Flex and CSS grid, borders, scrolling | LAYOUT-02 |
| `Avalonia.Controls.GridSplitter` | unmeasured | A static `role="separator"`; resizing needs script | LAYOUT-11 |
| `Avalonia.Controls.GroupBox` | unmeasured | `<fieldset>` with a `<legend>` | LAYOUT-15 |
| `Avalonia.Controls.HyperlinkButton` | unmeasured | `<a href>` | BUTTONS-02 |
| `Avalonia.Controls.Image` | unmeasured | `<img>` with a PNG data URI, or a lazy `<img src>` for a URL in AsyncImageLoader's `ImageLoader.Source`; `alt` from the automation name, `object-fit` from `Stretch` | MEDIA-06, MEDIA-09 |
| `Avalonia.Controls.ItemsControl` | unmeasured | The items panel, with the item template as a repeat slot | ENGINE-04 |
| `Avalonia.Controls.Label` | unmeasured | Text element; `Label` is a `<label>` for its target | TEXT-07 |
| `Avalonia.Controls.LayoutTransformControl` | unmeasured | A box of the transformed size with the child transformed inside | LAYOUT-13 |
| `Avalonia.Controls.ListBox` | unmeasured | Hidden radios (single) or checkboxes (multiple) in `<label>`s with the Fluent selected look; a two-way selection posts when it changes | ITEMS-02, ITEMS-03, ITEMS-04, ITEMS-18 |
| `Avalonia.Controls.ListBoxItem` | unmeasured | A `<label>` with a hidden radio or checkbox, selected look under `:has(:checked)` | ITEMS-02, ITEMS-03, ITEMS-04 |
| `Avalonia.Controls.MaskedTextBox` | unmeasured | `<input>` with pattern, `type=number` or a `<datalist>` | TEXT-10, TEXT-11 |
| `Avalonia.Controls.Menu` | unmeasured | `<menu>` toolbar whose items open anchored popover menus | NAVIGATION-03, NAVIGATION-06 |
| `Avalonia.Controls.MenuFlyoutPresenter` | unmeasured | Anchored `<div popover class="menu">` of menu items | NAVIGATION-04 |
| `Avalonia.Controls.MenuItem` | unmeasured | Submitting `<button>`, `<a>` link or a button opening a nested popover; icon, gesture, check, separators | NAVIGATION-04, NAVIGATION-05, NAVIGATION-06 |
| `Avalonia.Controls.NativeControlHost` | n/a | Hosts a native OS control |  |
| `Avalonia.Controls.NativeMenuBar` | n/a | Native OS menu bar |  |
| `Avalonia.Controls.NavigationPage` | unmeasured | `<header>` bar with the current page title and a back link (`a:Web.Href`); the stack is the browser history | NAVIGATION-16 |
| `Avalonia.Controls.Notifications.NotificationCard` | unmeasured | `<div role="status">` card with the type color bar | NAVIGATION-09 |
| `Avalonia.Controls.Notifications.WindowNotificationManager` | unmeasured | The cards a post showed, in a manual popover at the manager's corner; cards render through NotificationCard | ENGINE-147, SSR-155 |
| `Avalonia.Controls.NumericUpDown` | unmeasured | `<input>` with pattern, `type=number` or a `<datalist>` | TEXT-12, TEXT-13 |
| `Avalonia.Controls.PageNavigationHost` | unmeasured | Its page in its place | NAVIGATION-20 |
| `Avalonia.Controls.Panel` | unmeasured | One-cell CSS grid overlaying its children | LAYOUT-07 |
| `Avalonia.Controls.PathIcon` | unmeasured | Inline `<svg>` filled with `currentColor` | MEDIA-03 |
| `Avalonia.Controls.PipsPager` | unmeasured | Hidden radios as pips, previous and next as submit buttons | ITEMS-10 |
| `Avalonia.Controls.Presenters.ContentPresenter` | n/a | Template plumbing: its content is written in its place |  |
| `Avalonia.Controls.Presenters.ItemsPresenter` | n/a | Template plumbing: its panel is written in its place |  |
| `Avalonia.Controls.Presenters.ScrollContentPresenter` | n/a | Template plumbing of ScrollViewer |  |
| `Avalonia.Controls.Presenters.TableViewCellsPresenter` | n/a | Template plumbing of TableView |  |
| `Avalonia.Controls.Presenters.TableViewColumnHeadersPresenter` | n/a | Template plumbing of TableView |  |
| `Avalonia.Controls.Presenters.TextPresenter` | n/a | Template plumbing of TextBox |  |
| `Avalonia.Controls.Primitives.AccessText` | unmeasured | Text element; `Label` is a `<label>` for its target | TEXT-06 |
| `Avalonia.Controls.Primitives.AdornerLayer` | n/a | Internal adorner layer |  |
| `Avalonia.Controls.Primitives.CalendarButton` | unmeasured | Part of `Calendar`: a month or year cell of its table | RANGES-07 |
| `Avalonia.Controls.Primitives.CalendarDayButton` | unmeasured | Part of `Calendar`: a day cell of its table | RANGES-06 |
| `Avalonia.Controls.Primitives.CalendarItem` | unmeasured | Part of `Calendar`: its header and table | RANGES-06, RANGES-07 |
| `Avalonia.Controls.Primitives.DateTimePickerPanel` | n/a | Scrolling column of the date and time flyouts; the browser's native pickers replace it |  |
| `Avalonia.Controls.Primitives.HeaderedContentControl` | unmeasured | Its Fluent template (header row above content), or its own template, inside a flyout too | LAYOUT-16, NAVIGATION-26 |
| `Avalonia.Controls.Primitives.HeaderedItemsControl` | unmeasured | No theme in Fluent: generic template path | ITEMS-11 |
| `Avalonia.Controls.Primitives.HeaderedSelectingItemsControl` | unmeasured | No theme in Fluent: generic template path | ITEMS-11 |
| `Avalonia.Controls.Primitives.OverlayLayer` | n/a | Internal overlay layer |  |
| `Avalonia.Controls.Primitives.OverlayPopupHost` | n/a | Internal popup host |  |
| `Avalonia.Controls.Primitives.Popup` | n/a | Rendered through the control that owns it (flyouts, menus, tool tips) |  |
| `Avalonia.Controls.Primitives.PopupRoot` | n/a | Native popup window |  |
| `Avalonia.Controls.Primitives.ScrollBar` | unmeasured | Nothing: the browser draws the scroll bars | RANGES-14 |
| `Avalonia.Controls.Primitives.SelectingItemsControl` | unmeasured | No theme in Fluent: generic template path | ITEMS-11 |
| `Avalonia.Controls.Primitives.TabStrip` | unmeasured | CSS-only tabs: hidden radios in tab `<label>`s; a two-way selection posts when it changes | ITEMS-06, ITEMS-18 |
| `Avalonia.Controls.Primitives.TabStripItem` | unmeasured | A tab `<label>` with a hidden radio and the Fluent pipe | ITEMS-06 |
| `Avalonia.Controls.Primitives.TemplatedControl` | unmeasured | Any control: its template is rendered | LAYOUT-20 |
| `Avalonia.Controls.Primitives.TextSelectionHandle` | n/a | Touch text selection handle |  |
| `Avalonia.Controls.Primitives.TextSelectorLayer` | n/a | Touch text selection layer |  |
| `Avalonia.Controls.Primitives.Thumb` | unmeasured | Part of `Slider`: the range input's thumb, styled in CSS | RANGES-03 |
| `Avalonia.Controls.Primitives.ToggleButton` | unmeasured | `<label>` with a hidden checkbox and the checked look by `:has(:checked)` | BUTTONS-07 |
| `Avalonia.Controls.Primitives.Track` | unmeasured | Part of `Slider`: the range input's track and fill, styled in CSS | RANGES-03 |
| `Avalonia.Controls.Primitives.UniformGrid` | unmeasured | Flex and CSS grid, borders, scrolling | LAYOUT-03 |
| `Avalonia.Controls.Primitives.VisualLayerManager` | n/a | Internal layer manager |  |
| `Avalonia.Controls.ProgressBar` | unmeasured | `<progress>`, determinate or indeterminate, with its text | RANGES-04, RANGES-05 |
| `Avalonia.Controls.RadioButton` | unmeasured | `<input type=radio>` in a `<label>`, one name per group | BUTTONS-08, BUTTONS-09 |
| `Avalonia.Controls.RefreshContainer` | unmeasured | Its content only: pull to refresh is the browser's reload, which renders the page again | NAVIGATION-14 |
| `Avalonia.Controls.RefreshVisualizer` | n/a | Pull-to-refresh indicator: the browser draws its own when the page is pulled to reload |  |
| `Avalonia.Controls.RelativePanel` | different | Absolute positioning with CSS anchors, centered with auto margins; on ControlCatalog's RelativePanel page every box now sits where its references sit (chained `Below`, `Above` and `AlignRightWith` included), but the whole panel is 57.5 px higher than in Avalonia, whose `ScrollViewer` centers the fixed-height panel in its viewport (see `ScrollViewer`) | LAYOUT-09, bench:/relativepanel |
| `Avalonia.Controls.RepeatButton` | unmeasured | `<button type=submit>` running the command once per press | BUTTONS-06 |
| `Avalonia.Controls.ReversibleStackPanel` | unmeasured | Flex with a reversed direction | LAYOUT-10 |
| `Avalonia.Controls.ScrollViewer` | different | Flex and CSS grid, borders, scrolling (a `Hidden` bar's axis scrolls with no bar, `scrollbar-width:none`); a child smaller than the viewport is arranged in the viewport by Avalonia (a fixed-height child is centered vertically), while the web keeps it at the top | LAYOUT-05, LAYOUT-80, bench:/relativepanel |
| `Avalonia.Controls.SelectableTextBlock` | unmeasured | Text element; `Label` is a `<label>` for its target | TEXT-05 |
| `Avalonia.Controls.Separator` | unmeasured | `<hr>` with the theme stroke | LAYOUT-14 |
| `Avalonia.Controls.Shapes.Arc` | unmeasured | Inline `<svg>` path of its geometry laid out at its size; its rendered geometry inside a `Canvas` | MEDIA-05, MEDIA-12 |
| `Avalonia.Controls.Shapes.Ellipse` | equal | A CSS box with `border-radius:50%`; an `<svg>` `<ellipse>` inset by half its stroke inside a `Canvas` or with a size and a gradient | MEDIA-04, MEDIA-12, MEDIA-15, MEDIA-17, MEDIA-20, bench:/controls/vectors |
| `Avalonia.Controls.Shapes.Line` | equal | Inline `<svg>` path of its geometry with Avalonia's pen (miter limit 10, caps, dashes) | MEDIA-05, MEDIA-12, MEDIA-13, bench:/controls/vectors |
| `Avalonia.Controls.Shapes.Path` | equal | Inline `<svg>` path from the Skia geometry, view box from `Stretch`; its rendered geometry inside a `Canvas`; gradients on Avalonia's targets | MEDIA-02, MEDIA-12, MEDIA-13, MEDIA-14, MEDIA-18, bench:/controls/vectors |
| `Avalonia.Controls.Shapes.Polygon` | equal | Inline `<svg>` path of its geometry; a `<polygon>` of its points (bound points followed) inside a `Canvas` | MEDIA-05, MEDIA-12, bench:/controls/vectors |
| `Avalonia.Controls.Shapes.Polyline` | equal | Inline `<svg>` path of its geometry; a `<polyline>` of its points (bound points followed) inside a `Canvas` | MEDIA-05, MEDIA-13, MEDIA-19, bench:/controls/vectors |
| `Avalonia.Controls.Shapes.Rectangle` | equal | A CSS box (fill, stroke as border, radius; a gradient stroke as a gradient layer under the fill); an `<svg>` `<rect>` inset by half its stroke inside a `Canvas` or with a size and a gradient | MEDIA-04, MEDIA-11, MEDIA-12, MEDIA-14, MEDIA-17, LAYOUT-52, bench:/controls/vectors |
| `Avalonia.Controls.Shapes.Sector` | unmeasured | Inline `<svg>` path of its geometry laid out at its size; its rendered geometry inside a `Canvas` | MEDIA-05, MEDIA-12 |
| `Avalonia.Controls.Slider` | unmeasured | `<input type=range>` bound to `Value` on post, with the Fluent track and thumb | RANGES-01, RANGES-02, RANGES-03 |
| `Avalonia.Controls.SplitButton` | unmeasured | Submit button plus a chevron button opening its flyout popover | BUTTONS-12 |
| `Avalonia.Controls.SplitView` | unmeasured | Inline pane, or a popover drawer below a breakpoint | NAVIGATION-01 |
| `Avalonia.Controls.StackPanel` | unmeasured | Flex and CSS grid, borders, scrolling | LAYOUT-01, LAYOUT-24 |
| `Avalonia.Controls.TabControl` | unmeasured | CSS-only tabs: radio labels, the checked label's panel shown; bound tabs take each tab's own look and panel; a two-way selection posts when it changes | ITEMS-05, ITEMS-15, ITEMS-17, ITEMS-18 |
| `Avalonia.Controls.TabItem` | unmeasured | A tab `<label>` and its `.tp` panel | ITEMS-05 |
| `Avalonia.Controls.TabbedPage` | unmeasured | CSS-only tabs: radio buttons of one field, the checked one shows its page | NAVIGATION-17 |
| `Avalonia.Controls.TableView` | unmeasured | `<table>` with `<colgroup>`, `<thead>` and a row repeat | ITEMS-09 |
| `Avalonia.Controls.TableViewCell` | unmeasured | `<td>` | ITEMS-09 |
| `Avalonia.Controls.TableViewColumnHeader` | unmeasured | `<th>` | ITEMS-09 |
| `Avalonia.Controls.TableViewRow` | unmeasured | `<tr>` with the Fluent row look | ITEMS-09 |
| `Avalonia.Controls.TextBlock` | equal | Text element (`div`, `h1`, `h2` by class) with Avalonia's line height and font; inlines as spans | TEXT-01, TEXT-16, TEXT-17, bench:/controls/text |
| `Avalonia.Controls.TextBox` | unmeasured | `<input>` or `<textarea>`; two-way bound on post | TEXT-02, TEXT-03 |
| `Avalonia.Controls.ThemeVariantScope` | unmeasured | Its subtree resolved in its variant, with `color-scheme` | LAYOUT-17 |
| `Avalonia.Controls.TickBar` | unmeasured | Part of `Slider`: ticks drawn as a CSS gradient across the track | RANGES-15 |
| `Avalonia.Controls.TimePicker` | unmeasured | `<input type=time>` bound to `SelectedTime` on post | RANGES-12, RANGES-13 |
| `Avalonia.Controls.TimePickerPresenter` | n/a | Flyout of `TimePicker`; the browser's native time picker replaces it |  |
| `Avalonia.Controls.ToggleSplitButton` | unmeasured | Toggle label plus a chevron button opening its flyout popover | BUTTONS-13 |
| `Avalonia.Controls.ToggleSwitch` | unmeasured | `<input type=checkbox role=switch>` drawn as the Fluent switch | BUTTONS-10 |
| `Avalonia.Controls.ToolTip` | unmeasured | `<span role="tooltip">` shown by CSS on hover and focus, anchored to the control | NAVIGATION-08 |
| `Avalonia.Controls.TransitioningContentControl` | unmeasured | Content with a CSS enter animation from its `PageTransition` | NAVIGATION-12 |
| `Avalonia.Controls.TreeView` | unmeasured | Nested `<ul>` with `<details>`/`<summary>`; a bound tree is planned to any depth, each row indented by its depth | ITEMS-07, ITEMS-70 |
| `Avalonia.Controls.TreeViewItem` | unmeasured | `<li>`: `<details open>` when expanded, a row for leaves | ITEMS-07 |
| `Avalonia.Controls.UserControl` | unmeasured | Flex and CSS grid, borders, scrolling | LAYOUT-06 |
| `Avalonia.Controls.Viewbox` | equal | `<svg>` view box scaling its child in a `<foreignObject>`; a canvas of shapes inside draws as Avalonia draws it (Pictor's icon: 0.5% of pixels off in Chromium, 1.7% in Firefox, against its SVG) | MEDIA-07, MEDIA-18, bench:/controls/vectors |
| `Avalonia.Controls.VirtualizingCarouselPanel` | unmeasured | Written by its `Carousel` as scroll-snap slides | ITEMS-08 |
| `Avalonia.Controls.VirtualizingStackPanel` | unmeasured | Every item realized, as a flex stack | LAYOUT-19 |
| `Avalonia.Controls.Window` | n/a | The host window; the page is its content |  |
| `Avalonia.Controls.WindowBase` | n/a | Base of windows |  |
| `Avalonia.Controls.WrapPanel` | different | Flex wrap; a centered `WrapPanel` is as wide as its widest row in Avalonia, but CSS cannot shrink-wrap wrapped content, so with a `MaxWidth` it takes the whole `MaxWidth` (ControlCatalog's TextBlock page: 680 px against 660, texts 10 px to the left) | LAYOUT-03, bench:/textblock |

## States (`state`)

Pseudo-classes declared with `[PseudoClasses]` on Avalonia's public types.

| Construct | Status | Reason | Criteria and evidence |
| --- | --- | --- | --- |
| `:blackout` | not supported | Blackout dates of a `Calendar` are not marked |  |
| `:bottom` | not supported | SplitView pane placement other than left is not written |  |
| `:bottomcenter` | n/a | Position of a window notification host; cards render through `NotificationCard` |  |
| `:bottomleft` | n/a | Position of a window notification host; cards render through `NotificationCard` |  |
| `:bottomright` | n/a | Position of a window notification host; cards render through `NotificationCard` |  |
| `:btnfocused` | n/a | Keyboard focus inside Avalonia's calendar; the web calendar is a static table |  |
| `:calendardisabled` | not supported | A disabled `Calendar`'s look is not written |  |
| `:checked` | unmeasured | The checked look follows a hidden input through `:has(:checked)` | BUTTONS-07, BUTTONS-13, NAVIGATION-11 |
| `:closed` | unmeasured | The closed pane is the popover drawer's default | NAVIGATION-01 |
| `:compactinline` | not supported | SplitView compact display modes are not written |  |
| `:compactoverlay` | not supported | SplitView compact display modes are not written |  |
| `:dayfocused` | n/a | Keyboard focus inside Avalonia's calendar; the web calendar is a static table |  |
| `:detail-is-navpage` | not supported | DrawerPage with a NavigationPage detail is written as its parts only |  |
| `:disabled` | unmeasured | The disabled look is Avalonia's at compile time; the element carries `disabled`, and no state look applies to it | ENGINE-28, ENGINE-57, BUTTONS-01, BUTTONS-19, TEXT-08 |
| `:down` | unmeasured | The expand direction is Avalonia's look at compile time | NAVIGATION-02 |
| `:dragging` | n/a | Dragging a `ToggleSwitch` knob; the browser toggles the checkbox on click |  |
| `:dropdownopen` | n/a | The browser draws the open `<select>` list or `<datalist>` suggestions |  |
| `:empty` | unmeasured | Empty content or lists are written as Avalonia renders them; a `TextBox` placeholder is `::placeholder` | ENGINE-19 |
| `:error` | unmeasured | Validation errors after a post; error notification cards | NAVIGATION-13, NAVIGATION-09 |
| `:expanded` | unmeasured | `<details open>` | NAVIGATION-02 |
| `:first-page` | unmeasured | The previous button is hidden on the first page | ITEMS-10 |
| `:flyout-open` | not supported | The button's look while its popover is open is not written |  |
| `:focus` | different | Text boxes carry Fluent's focused look under `:focus`; other controls keep the browser's focus ring, which Avalonia's capture never draws (its focus adorner needs real keyboard focus), so with focus forced on every enabled element the Gallery's text colors match 84.3% (90.8% in the normal state) and content pixels 91.7% (98.0%) | TEXT-09, ENGINE-55, ENGINE-57, bench:/controls/text |
| `:focus-visible` | different | Some looks are written under `:focus-visible`; Avalonia's focus adorner is not drawn, and the browser draws its own outline (measured with `:focus` in the bench's focus state) | ENGINE-55, bench:/controls/buttons |
| `:focus-within` | unmeasured | Used for tool tips shown on keyboard focus | NAVIGATION-08 |
| `:fullscreen` | n/a | Window chrome drawn by Avalonia; on the web the browser draws the window |  |
| `:has-border` | n/a | Window chrome drawn by Avalonia; on the web the browser draws the window |  |
| `:has-fullscreen` | n/a | Window chrome drawn by Avalonia; on the web the browser draws the window |  |
| `:has-maximize` | n/a | Window chrome drawn by Avalonia; on the web the browser draws the window |  |
| `:has-minimize` | n/a | Window chrome drawn by Avalonia; on the web the browser draws the window |  |
| `:has-shadow` | n/a | Window chrome drawn by Avalonia; on the web the browser draws the window |  |
| `:has-titlebar` | n/a | Window chrome drawn by Avalonia; on the web the browser draws the window |  |
| `:hasnodate` | unmeasured | An empty `<input type=date>` shows the browser's own placeholder | RANGES-10 |
| `:hasnotime` | unmeasured | An empty `<input type=time>` shows the browser's own placeholder | RANGES-12 |
| `:horizontal` | unmeasured |  | RANGES-03, ITEMS-10 |
| `:icon` | unmeasured | Menu item with an icon | NAVIGATION-05 |
| `:inactive` | unmeasured | Days of other months marked inactive | RANGES-06 |
| `:indeterminate` | unmeasured |  | BUTTONS-15, RANGES-05 |
| `:information` | unmeasured | Card color bar by notification type | NAVIGATION-09 |
| `:inline` | unmeasured | Inline pane above the breakpoint | NAVIGATION-01 |
| `:last-page` | not supported | The next button is not hidden on the last page |  |
| `:left` | unmeasured | Expand direction or spinner location, as Avalonia lays it out | NAVIGATION-02, BUTTONS-14 |
| `:lightDismiss` | unmeasured | Light dismiss is the popover's own behavior | NAVIGATION-01 |
| `:maximized` | n/a | Window chrome drawn by Avalonia; on the web the browser draws the window |  |
| `:nav-bar-compact` | not supported | NavigationPage bar modes are not written |  |
| `:nav-bar-inset` | not supported | NavigationPage bar modes are not written |  |
| `:normal` | n/a | Window chrome drawn by Avalonia; on the web the browser draws the window |  |
| `:open` | unmeasured | Popovers open in the browser; the open look of the opener is not written | NAVIGATION-01, NAVIGATION-06, NAVIGATION-08 |
| `:overlay` | unmeasured | Popover drawer below the breakpoint | NAVIGATION-01 |
| `:placement-bottom` | not supported | DrawerPage placement other than the default is not written |  |
| `:placement-right` | not supported | DrawerPage placement other than the default is not written |  |
| `:placement-top` | not supported | DrawerPage placement other than the default is not written |  |
| `:pointerover` | different | Written as `:hover` rules computed by Avalonia with the pseudo-class set, for containers, links and items with state styles; with pointer-over forced on every enabled element the Gallery's text colors match 89.6% against 90.8% in the normal state; the rest is a top-level `MenuItem`, whose highlight follows `:selected` in Avalonia | MOTION-04, TEXT-09, BUTTONS-20, ENGINE-55, ENGINE-57, bench:/controls/buttons |
| `:pressed` | different | Written as `:active` rules computed by Avalonia with the pseudo-class set, selected items included; forced with pointer-over, the Gallery's text colors match 89.8% and content pixels 97.2% (90.8% and 98.0% normal) | MOTION-04, BUTTONS-20, ITEMS-14, ENGINE-55, bench:/controls/items |
| `:radio` | not supported | Radio menu items are written as toggles |  |
| `:right` | unmeasured | Expand direction or spinner location, as Avalonia lays it out | NAVIGATION-02, BUTTONS-14 |
| `:selected` | unmeasured | Follows a hidden radio or checkbox, or `aria-selected` on calendar cells | ITEMS-02, ITEMS-05, ITEMS-07, RANGES-06 |
| `:separator` | unmeasured | Menu separators | NAVIGATION-05 |
| `:singleitem` | n/a | Internal marker of Avalonia's items control template |  |
| `:success` | unmeasured | Card color bar by notification type | NAVIGATION-09 |
| `:today` | unmeasured |  | RANGES-06 |
| `:toggle` | unmeasured | Menu item toggle | NAVIGATION-05 |
| `:top` | not supported | SplitView pane placement other than left is not written |  |
| `:topcenter` | n/a | Position of a window notification host; cards render through `NotificationCard` |  |
| `:topleft` | n/a | Position of a window notification host; cards render through `NotificationCard` |  |
| `:topright` | n/a | Position of a window notification host; cards render through `NotificationCard` |  |
| `:touch-mode` | n/a | Touch-specific `TextBox` look; the browser handles touch input |  |
| `:unchecked` | unmeasured | The unchecked look is the look without `:checked` | BUTTONS-07 |
| `:up` | unmeasured | The expand direction is Avalonia's look at compile time | NAVIGATION-02 |
| `:vertical` | unmeasured | A vertical slider is laid out vertically | RANGES-03 |
| `:visited` | not supported | `HyperlinkButton` visited look is not written as `:visited` |  |
| `:warning` | unmeasured | Card color bar by notification type | NAVIGATION-09 |

## Panels and layout properties (`layout`)

Settable styled and attached properties of the layout types.

| Construct | Status | Reason | Criteria and evidence |
| --- | --- | --- | --- |
| `Border.Background` | equal | `background` with the same color; bound on a border, a panel or a control whose element is its frame (buttons, text boxes, combo boxes), its inline style | LAYOUT-04, LAYOUT-17, ENGINE-159 |
| `Border.BackgroundSizing` | not supported | Always the CSS default (`background-clip:border-box`) |  |
| `Border.BorderBrush` | equal | `border-color` | LAYOUT-04 |
| `Border.BorderThickness` | equal | `border-width` | LAYOUT-04 |
| `Border.BoxShadow` | unmeasured | `box-shadow`; blur radii are defined differently by Skia and CSS | LAYOUT-04, ENGINE-32 |
| `Border.CornerRadius` | equal | `border-radius` | LAYOUT-04, ENGINE-36, ENGINE-159 |
| `Canvas.Bottom` | equal | The child is placed at the canvas height minus its height minus the offset, as Avalonia arranges it | MEDIA-11 |
| `Canvas.Left` | equal | The child is placed at its offset; unset means 0 (or `Canvas.Right`) | MEDIA-01, MEDIA-11 |
| `Canvas.Right` | equal | The child is placed at the canvas width minus its width minus the offset, as Avalonia arranges it | MEDIA-11 |
| `Canvas.Top` | equal | The child is placed at its offset; unset means 0 (or `Canvas.Bottom`) | MEDIA-01, MEDIA-11 |
| `ColumnDefinition.MaxWidth` | not supported | Tracks are written from `Width` only (would be `minmax()`) |  |
| `ColumnDefinition.MinWidth` | not supported | Tracks are written from `Width` only (would be `minmax()`) |  |
| `ColumnDefinition.Width` | unmeasured | Pixels, `auto`, stars as `minmax(0,Nfr)` | LAYOUT-02 |
| `Decorator.Child` | unmeasured |  | LAYOUT-06 |
| `Decorator.Padding` | unmeasured | `padding` | LAYOUT-04, ENGINE-36, ENGINE-159 |
| `DefinitionBase.SharedSizeGroup` | not supported | Each grid is its own CSS grid; shared sizes across grids are not written |  |
| `DockPanel.Dock` | unmeasured |  | LAYOUT-08 |
| `DockPanel.HorizontalSpacing` | unmeasured |  | LAYOUT-08 |
| `DockPanel.LastChildFill` | unmeasured |  | LAYOUT-08 |
| `DockPanel.VerticalSpacing` | unmeasured |  | LAYOUT-08 |
| `Grid.Column` | unmeasured |  | LAYOUT-02, ENGINE-31 |
| `Grid.ColumnSpacing` | unmeasured | Written as `column-gap`; no criterion yet |  |
| `Grid.ColumnSpan` | unmeasured |  | LAYOUT-02, ENGINE-31 |
| `Grid.IsSharedSizeScope` | not supported | Each grid is its own CSS grid; shared sizes across grids are not written |  |
| `Grid.Row` | unmeasured |  | LAYOUT-02, ENGINE-31 |
| `Grid.RowSpacing` | unmeasured | Written as `row-gap`; no criterion yet |  |
| `Grid.RowSpan` | unmeasured |  | LAYOUT-02, ENGINE-31 |
| `Grid.ShowGridLines` | n/a | Debugging aid of Avalonia's grid |  |
| `LayoutTransformControl.LayoutTransform` | unmeasured |  | LAYOUT-13 |
| `LayoutTransformControl.UseRenderTransform` | not supported | Always written as a layout transform |  |
| `Layoutable.Height` | unmeasured | `height`; a bound height is the inline style of containers, text and controls written with the shared root writer | LAYOUT-24, ENGINE-158 |
| `Layoutable.HorizontalAlignment` | unmeasured | Flex or grid self-alignment | LAYOUT-07, LAYOUT-24, ENGINE-31 |
| `Layoutable.Margin` | unmeasured | `margin` | ENGINE-32, ENGINE-158 |
| `Layoutable.MaxHeight` | not supported | Written only for list boxes, not for any control |  |
| `Layoutable.MaxWidth` | unmeasured | Written as `max-width`, centered when stretched; no criterion yet |  |
| `Layoutable.MinHeight` | not supported | Written only in some control looks, not for any control |  |
| `Layoutable.MinWidth` | unmeasured | Written as `min-width`; no criterion yet |  |
| `Layoutable.UseLayoutRounding` | unmeasured | Avalonia rounds to device pixels; the browser lays out in fractions and snaps at paint |  |
| `Layoutable.VerticalAlignment` | unmeasured | Flex or grid self-alignment | LAYOUT-07, LAYOUT-24, ENGINE-31 |
| `Layoutable.Width` | unmeasured | `width`; a bound width is the inline style of containers, text and controls written with the shared root writer | LAYOUT-24, ENGINE-158 |
| `Panel.Background` | unmeasured | `background` | LAYOUT-07 |
| `RelativePanel.Above` | unmeasured | CSS anchor positioning | LAYOUT-09 |
| `RelativePanel.AlignBottomWith` | unmeasured | CSS anchor positioning | LAYOUT-09 |
| `RelativePanel.AlignBottomWithPanel` | unmeasured | CSS anchor positioning | LAYOUT-09 |
| `RelativePanel.AlignHorizontalCenterWith` | unmeasured | CSS anchor positioning | LAYOUT-09 |
| `RelativePanel.AlignHorizontalCenterWithPanel` | unmeasured | CSS anchor positioning | LAYOUT-09 |
| `RelativePanel.AlignLeftWith` | unmeasured | CSS anchor positioning | LAYOUT-09 |
| `RelativePanel.AlignLeftWithPanel` | unmeasured | CSS anchor positioning | LAYOUT-09 |
| `RelativePanel.AlignRightWith` | unmeasured | CSS anchor positioning | LAYOUT-09 |
| `RelativePanel.AlignRightWithPanel` | unmeasured | CSS anchor positioning | LAYOUT-09 |
| `RelativePanel.AlignTopWith` | unmeasured | CSS anchor positioning | LAYOUT-09 |
| `RelativePanel.AlignTopWithPanel` | unmeasured | CSS anchor positioning | LAYOUT-09 |
| `RelativePanel.AlignVerticalCenterWith` | unmeasured | CSS anchor positioning | LAYOUT-09 |
| `RelativePanel.AlignVerticalCenterWithPanel` | unmeasured | CSS anchor positioning | LAYOUT-09 |
| `RelativePanel.Below` | unmeasured | CSS anchor positioning | LAYOUT-09 |
| `RelativePanel.LeftOf` | unmeasured | CSS anchor positioning | LAYOUT-09 |
| `RelativePanel.RightOf` | unmeasured | CSS anchor positioning | LAYOUT-09 |
| `RowDefinition.Height` | unmeasured | Pixels, `auto`, stars as `minmax(0,Nfr)` | LAYOUT-02 |
| `RowDefinition.MaxHeight` | not supported | Tracks are written from `Height` only (would be `minmax()`) |  |
| `RowDefinition.MinHeight` | not supported | Tracks are written from `Height` only (would be `minmax()`) |  |
| `ScrollViewer.AllowAutoHide` | n/a | The browser decides how its scroll bars hide |  |
| `ScrollViewer.BringIntoViewOnFocusChange` | n/a | The browser scrolls focused elements into view |  |
| `ScrollViewer.HorizontalScrollBarVisibility` | unmeasured | `overflow-x:auto`; the browser draws the scroll bar (RANGES-14) | LAYOUT-05 |
| `ScrollViewer.HorizontalSnapPointsAlignment` | not supported | Snap points are written only by carousels (ITEMS-08) |  |
| `ScrollViewer.HorizontalSnapPointsType` | not supported | Snap points are written only by carousels (ITEMS-08) |  |
| `ScrollViewer.IsDeferredScrollingEnabled` | n/a | Scrolling behavior of the browser |  |
| `ScrollViewer.IsScrollChainingEnabled` | n/a | Scrolling behavior of the browser (`overscroll-behavior`) |  |
| `ScrollViewer.IsScrollInertiaEnabled` | n/a | Scrolling behavior of the browser |  |
| `ScrollViewer.Offset` | not supported | The initial scroll offset is not written |  |
| `ScrollViewer.VerticalScrollBarVisibility` | not supported | An inner vertical scroll viewer is not written; the document scrolls |  |
| `ScrollViewer.VerticalSnapPointsAlignment` | not supported | Snap points are written only by carousels (ITEMS-08) |  |
| `ScrollViewer.VerticalSnapPointsType` | not supported | Snap points are written only by carousels (ITEMS-08) |  |
| `StackPanel.AreHorizontalSnapPointsRegular` | n/a | Scroll snapping hint of Avalonia's scroll viewer |  |
| `StackPanel.AreVerticalSnapPointsRegular` | n/a | Scroll snapping hint of Avalonia's scroll viewer |  |
| `StackPanel.Orientation` | unmeasured |  | LAYOUT-01 |
| `StackPanel.Spacing` | unmeasured |  | LAYOUT-01 |
| `UniformGrid.ColumnSpacing` | unmeasured | Written as `column-gap`; no criterion yet |  |
| `UniformGrid.Columns` | unmeasured |  | LAYOUT-03 |
| `UniformGrid.FirstColumn` | not supported | Leading empty cells are not written |  |
| `UniformGrid.RowSpacing` | unmeasured | Written as `row-gap`; no criterion yet |  |
| `UniformGrid.Rows` | unmeasured | One row is written as a single row of columns; other row counts follow the columns | LAYOUT-03 |
| `Viewbox.Child` | equal |  | MEDIA-07, MEDIA-18, bench:/controls/vectors |
| `Viewbox.Stretch` | equal | `preserveAspectRatio` | MEDIA-07, bench:/controls/vectors |
| `Viewbox.StretchDirection` | unmeasured | Written as minimum or maximum sizes; no criterion yet |  |
| `Visual.CacheMode` | n/a | Compositor bitmap cache; nothing to draw |  |
| `Visual.Clip` | not supported | Geometry clips are not written (would be `clip-path`) |  |
| `Visual.ClipToBounds` | equal | `overflow:hidden` | LAYOUT-22 |
| `Visual.Effect` | not supported | Effects are not written (would be `filter`) |  |
| `Visual.FlowDirection` | not supported | Right-to-left is not written (would be `dir=rtl`) |  |
| `Visual.IsVisible` | equal | Invisible elements are not written | ENGINE-03 |
| `Visual.Opacity` | equal | `opacity` | LAYOUT-22, ENGINE-32, ENGINE-158 |
| `Visual.OpacityMask` | not supported | Opacity masks are not written (would be `mask-image`) |  |
| `Visual.RenderTransform` | equal | `transform:matrix()` with Avalonia's matrix | LAYOUT-12, ENGINE-37, ENGINE-158 |
| `Visual.RenderTransformOrigin` | equal | `transform-origin` | LAYOUT-12 |
| `Visual.ZIndex` | equal | `z-index` | LAYOUT-22 |
| `WrapPanel.ItemHeight` | not supported | Uniform item sizes are not written |  |
| `WrapPanel.ItemSpacing` | unmeasured |  | LAYOUT-03 |
| `WrapPanel.ItemWidth` | not supported | Uniform item sizes are not written |  |
| `WrapPanel.ItemsAlignment` | not supported | Line alignment is not written (would be `justify-content`) |  |
| `WrapPanel.LineSpacing` | unmeasured |  | LAYOUT-03 |
| `WrapPanel.Orientation` | not supported | Only the horizontal wrap is written |  |

## Text (`text`)

Settable properties of `TextBlock` and `TextElement`, and every inline.

| Construct | Status | Reason | Criteria and evidence |
| --- | --- | --- | --- |
| `Avalonia.Controls.Documents.Bold` | unmeasured | `<b>` | TEXT-04 |
| `Avalonia.Controls.Documents.InlineUIContainer` | unmeasured | Its child written in place | TEXT-04 |
| `Avalonia.Controls.Documents.Italic` | unmeasured | `<i>` | TEXT-04 |
| `Avalonia.Controls.Documents.LineBreak` | unmeasured | `<br>` | TEXT-04 |
| `Avalonia.Controls.Documents.Run` | unmeasured |  | TEXT-04 |
| `Avalonia.Controls.Documents.Span` | unmeasured | `<span>` | TEXT-04 |
| `Avalonia.Controls.Documents.Underline` | unmeasured | `<u>` | TEXT-04 |
| `TextBlock.Background` | not supported | Not written on a text block |  |
| `TextBlock.BaselineOffset` | not supported | Not written |  |
| `TextBlock.FontFamily` | not supported | The page font is Inter, as Avalonia's default; other families are not written |  |
| `TextBlock.FontFeatures` | not supported | Not written (would be `font-feature-settings`) |  |
| `TextBlock.FontSize` | equal | `font-size`, with Avalonia's line height for it rounded up to a pixel | TEXT-01, TEXT-16, ENGINE-32 |
| `TextBlock.FontStretch` | not supported | Not written (would be `font-stretch`) |  |
| `TextBlock.FontStyle` | not supported | Written for inlines only, not on the text block |  |
| `TextBlock.FontWeight` | unmeasured |  | TEXT-01 |
| `TextBlock.Foreground` | unmeasured |  | TEXT-01 |
| `TextBlock.Inlines` | unmeasured |  | TEXT-04 |
| `TextBlock.LetterSpacing` | not supported | Not written (would be `letter-spacing`) |  |
| `TextBlock.LineHeight` | unmeasured | Written as `line-height`; no criterion yet |  |
| `TextBlock.LineSpacing` | not supported | Not written |  |
| `TextBlock.MaxLines` | not supported | Not written (would be `line-clamp`) |  |
| `TextBlock.Padding` | equal | `padding` on the text element | TEXT-17 |
| `TextBlock.Text` | equal | The same Inter files as Avalonia, so the same advances (widths within 0.02 px); glyphs are rasterized by each engine and the bench tolerates antialiasing | TEXT-01, RENDERING-02, SSR-18, bench:/controls/text |
| `TextBlock.TextAlignment` | unmeasured | Left, center and right; `Justify` is not written | TEXT-01 |
| `TextBlock.TextDecorations` | unmeasured | Written for hyperlinks and inlines; not on a plain text block | BUTTONS-02 |
| `TextBlock.TextTrimming` | unmeasured | `text-overflow:ellipsis` for every trimming mode | TEXT-01 |
| `TextBlock.TextWrapping` | unmeasured | `white-space:pre` or `pre-wrap`; line breaking rules differ between engines | TEXT-01 |
| `TextElement.Background` | unmeasured | Set on spans | TEXT-04 |
| `TextElement.FontFamily` | not supported | The page font is Inter; other families are not written |  |
| `TextElement.FontFeatures` | not supported | Not written |  |
| `TextElement.FontSize` | unmeasured | Inherited and set on spans; bound on a text or on a control with text content, its inline style | TEXT-04, ENGINE-158 |
| `TextElement.FontStretch` | not supported | Not written |  |
| `TextElement.FontStyle` | unmeasured | Set on spans | TEXT-04 |
| `TextElement.FontWeight` | unmeasured | Inherited and set on spans | TEXT-04 |
| `TextElement.Foreground` | unmeasured | Inherited and set on spans; bound on a text or on a control with text content, its inline style | TEXT-04, ENGINE-158 |
| `TextElement.LetterSpacing` | not supported | Not written |  |

## Selectors (`selector`)

The selector operators of `Avalonia.Styling.Selectors`.

| Construct | Status | Reason | Criteria and evidence |
| --- | --- | --- | --- |
| `Selectors.Child` | unmeasured | Applied by Avalonia's styling when the page is compiled; the web gets the computed look | ENGINE-16 |
| `Selectors.Class` | unmeasured | Applied by Avalonia's styling when the page is compiled; the web gets the computed look; bound classes are switched by the plan | ENGINE-24, MOTION-01 |
| `Selectors.Descendant` | unmeasured | Applied by Avalonia's styling when the page is compiled; the web gets the computed look | MOTION-04, ENGINE-24 |
| `Selectors.Is` | unmeasured | Applied by Avalonia's styling when the page is compiled; the web gets the computed look | ENGINE-16 |
| `Selectors.Name` | unmeasured | Applied by Avalonia's styling when the page is compiled; the web gets the computed look | ENGINE-16 |
| `Selectors.Nesting` | unmeasured | Applied by Avalonia's styling when the page is compiled; the web gets the computed look | ENGINE-39 |
| `Selectors.Not` | unmeasured | Applied by Avalonia's styling when the page is compiled; the web gets the computed look | ENGINE-16 |
| `Selectors.NthChild` | unmeasured | Applied by Avalonia's styling when the page is compiled; the web gets the computed look; in a repeated list the page is rendered literally when items differ | ENGINE-16 |
| `Selectors.NthLastChild` | unmeasured | Applied by Avalonia's styling when the page is compiled; the web gets the computed look; in a repeated list the page is rendered literally when items differ | ENGINE-16 |
| `Selectors.OfType` | unmeasured | Applied by Avalonia's styling when the page is compiled; the web gets the computed look | MOTION-01, ENGINE-18 |
| `Selectors.Or` | unmeasured | Applied by Avalonia's styling when the page is compiled; the web gets the computed look | ENGINE-16 |
| `Selectors.PropertyEquals` | unmeasured | Applied by Avalonia's styling when the page is compiled; the web gets the computed look; a bound `Tag` read by selectors is switched by the plan | ENGINE-39 |
| `Selectors.Template` | unmeasured | Applied by Avalonia's styling when the page is compiled; the web gets the computed look; Fluent's looks reach template parts this way | LAYOUT-20, BUTTONS-05 |

## Styles, themes and resources (`styling`)

Styles, control themes, resource dictionaries and includes, and the theme variants.

| Construct | Status | Reason | Criteria and evidence |
| --- | --- | --- | --- |
| `Avalonia.Controls.ResourceDictionary` | unmeasured | Resolved by Avalonia per theme; one plan per theme | LAYOUT-17, CHARTS-06 |
| `Avalonia.Markup.Xaml.Styling.MergeResourceInclude` | unmeasured | Resolved by Avalonia per theme | LAYOUT-17 |
| `Avalonia.Markup.Xaml.Styling.ResourceInclude` | unmeasured | Resolved by Avalonia per theme | LAYOUT-17 |
| `Avalonia.Markup.Xaml.Styling.StyleInclude` | unmeasured | Applied by Avalonia's styling when the page is compiled; the web gets the computed look | ENGINE-16 |
| `Avalonia.Styling.ContainerQuery` | different | Width queries written as CSS `@container` on an inline-size container, each state traced at the width it asks for, widths less the container's padding and border; height queries evaluated once at the compile width; a container in a horizontal stack, which Avalonia measures at its content's width, collapses under CSS inline-size containment | LAYOUT-81, LAYOUT-82, LAYOUT-83 |
| `Avalonia.Styling.ControlTheme` | unmeasured | Applied by Avalonia's styling when the page is compiled; the web gets the computed look | ENGINE-18, LAYOUT-20 |
| `Avalonia.Styling.Style` | unmeasured | Applied by Avalonia's styling when the page is compiled; the web gets the computed look | ENGINE-16, MOTION-01 |
| `Avalonia.Styling.Styles` | unmeasured | Applied by Avalonia's styling when the page is compiled; the web gets the computed look | ENGINE-16 |
| `ThemeVariant.Dark` | unmeasured | One plan per theme, with `color-scheme:dark` | ENGINE-05, LAYOUT-17 |
| `ThemeVariant.Default` | unmeasured | The page is rendered in the light or the dark theme chosen by the visitor | ENGINE-05 |
| `ThemeVariant.Light` | unmeasured | One plan per theme | ENGINE-05, LAYOUT-17 |

## Bindings (`binding`)

Binding kinds, modes and relative sources.

| Construct | Status | Reason | Criteria and evidence |
| --- | --- | --- | --- |
| `Avalonia.Data.Binding` | equal | The plan writes what Avalonia's binding writes for each model; an explicit `Source` that is not the page's data is written as rendered, as a resource is | ENGINE-01, ENGINE-20, ENGINE-25, ENGINE-60, ENGINE-61 |
| `Avalonia.Data.CompiledBinding` | equal | The plan writes what Avalonia's binding writes for each model; a `Command` bound to a method runs it as Avalonia's `MethodToCommandConverter` does, and one bound to a method of another element (`$parent[IClearControl].Clear`) posts through the Avalonia engine; the stream operator (`Title^`) shows an observable's latest value or a task's result | ENGINE-01, ENGINE-30, ENGINE-63, ENGINE-120, ENGINE-121 |
| `Avalonia.Data.MultiBinding` | equal |  | ENGINE-27 |
| `Avalonia.Data.ReflectionBinding` | unmeasured | Read through the same binding expressions; reflection is not used on the request path once planned | ENGINE-01 |
| `Avalonia.Data.TemplateBinding` | unmeasured | Resolved by Avalonia when the template is applied; a `MultiBinding` of `TemplateBinding`s of properties the page sets (an item declared in the XAML included) is written as rendered | LAYOUT-20, ENGINE-98 |
| `BindingMode.Default` | equal |  | ENGINE-01 |
| `BindingMode.OneTime` | unmeasured | Every render reads the current value; no criterion yet |  |
| `BindingMode.OneWay` | equal |  | ENGINE-01 |
| `BindingMode.OneWayToSource` | unmeasured | Written back on post like two-way; no criterion yet |  |
| `BindingMode.TwoWay` | equal | Read on render, written back on post | ENGINE-07, TEXT-03 |
| `RelativeSourceMode.DataContext` | equal |  | ENGINE-01, ENGINE-29 |
| `RelativeSourceMode.FindAncestor` | equal |  | ENGINE-30, ENGINE-34 |
| `RelativeSourceMode.Self` | unmeasured | Resolved by Avalonia; no criterion yet |  |
| `RelativeSourceMode.TemplatedParent` | unmeasured | Resolved by Avalonia when the template is applied; a property the page sets on the templated control is written as rendered | LAYOUT-20, ENGINE-98 |

## Templates (`template`)

Data, control and panel templates.

An app's own template selector (a class implementing `IDataTemplate`, Avalonia's `DataTemplateSelector` pattern) has no row, since the inventory lists Avalonia's types, but it is equal: content with an enum or a `bool` is learned once per value whatever its `Build` runs, and a selector that holds templates (a dictionary by key, a default template) is rebuilt by generated code that runs its own `Match` and `Build` on each item.

A selector that builds controls in code for other content, or that cannot be rebuilt, leaves its page to the engine with a reason naming it; an item its `Match` refuses writes nothing, where Avalonia falls back to the hosts' templates.

| Construct | Status | Reason | Criteria and evidence |
| --- | --- | --- | --- |
| `Avalonia.Controls.Templates.FuncControlTemplate` | unmeasured | Realized by Avalonia like a XAML control template; no criterion yet |  |
| `Avalonia.Controls.Templates.FuncDataTemplate` | unmeasured | Realized by Avalonia like a XAML data template; no criterion yet |  |
| `Avalonia.Controls.Templates.FuncTreeDataTemplate` | unmeasured | Realized by Avalonia; no criterion yet |  |
| `Avalonia.Markup.Xaml.Templates.ControlTemplate` | unmeasured |  | LAYOUT-20 |
| `Avalonia.Markup.Xaml.Templates.DataTemplate` | equal | The plan repeats the template Avalonia realized | ENGINE-04, ENGINE-26, ENGINE-132, ITEMS-17, BUTTONS-21 |
| `Avalonia.Markup.Xaml.Templates.FocusAdornerTemplate` | not supported | Avalonia's focus adorner is not drawn; the browser draws its focus ring |  |
| `Avalonia.Markup.Xaml.Templates.ItemsPanelTemplate` | unmeasured |  | LAYOUT-19 |
| `Avalonia.Markup.Xaml.Templates.Template` | unmeasured | Realized by Avalonia; no criterion yet |  |
| `Avalonia.Markup.Xaml.Templates.TreeDataTemplate` | unmeasured |  | ITEMS-07 |
| `Avalonia.Markup.Xaml.Templates.WindowDrawnDecorationsTemplate` | n/a | Window chrome drawn by Avalonia; on the web the browser draws the window |  |

## Transitions and animations (`animation`)

Transitions, key-frame animations, easings and page transitions.

| Construct | Status | Reason | Criteria and evidence |
| --- | --- | --- | --- |
| `Avalonia.Animation.Animation` | unmeasured | CSS `@keyframes` animation | MOTION-01, MOTION-02 |
| `Avalonia.Animation.BoolTransition` | not supported | Not written as a CSS transition |  |
| `Avalonia.Animation.BoxShadowsTransition` | not supported | Not written as a CSS transition |  |
| `Avalonia.Animation.BrushTransition` | unmeasured | Background, foreground and border brush | MOTION-03 |
| `Avalonia.Animation.ColorTransition` | not supported | Not written as a CSS transition |  |
| `Avalonia.Animation.CompositePageTransition` | unmeasured | Its fade and slide combined | NAVIGATION-12 |
| `Avalonia.Animation.CornerRadiusTransition` | not supported | Not written as a CSS transition |  |
| `Avalonia.Animation.CrossFade` | unmeasured | CSS enter animation | NAVIGATION-12 |
| `Avalonia.Animation.DoubleTransition` | unmeasured | Opacity, width and height | MOTION-03 |
| `Avalonia.Animation.Easings.BackEaseIn` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.BackEaseInOut` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.BackEaseOut` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.BounceEaseIn` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.BounceEaseInOut` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.BounceEaseOut` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.CircularEaseIn` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.CircularEaseInOut` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.CircularEaseOut` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.CubicEaseIn` | unmeasured | The same cubic Bezier curve in CSS | MOTION-01, MOTION-03 |
| `Avalonia.Animation.Easings.CubicEaseInOut` | unmeasured | The same cubic Bezier curve in CSS | MOTION-01, MOTION-03 |
| `Avalonia.Animation.Easings.CubicEaseOut` | unmeasured | The same cubic Bezier curve in CSS | MOTION-01, MOTION-03 |
| `Avalonia.Animation.Easings.ElasticEaseIn` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.ElasticEaseInOut` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.ElasticEaseOut` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.ExponentialEaseIn` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.ExponentialEaseInOut` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.ExponentialEaseOut` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.LinearEasing` | unmeasured | The same cubic Bezier curve in CSS | MOTION-01, MOTION-03 |
| `Avalonia.Animation.Easings.QuadraticEaseIn` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.QuadraticEaseInOut` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.QuadraticEaseOut` | unmeasured | The same cubic Bezier curve in CSS | MOTION-01, MOTION-03 |
| `Avalonia.Animation.Easings.QuarticEaseIn` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.QuarticEaseInOut` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.QuarticEaseOut` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.QuinticEaseIn` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.QuinticEaseInOut` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.QuinticEaseOut` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.SineEaseIn` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.SineEaseInOut` | unmeasured | The same cubic Bezier curve in CSS | MOTION-01, MOTION-03 |
| `Avalonia.Animation.Easings.SineEaseOut` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.SplineEasing` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.Easings.SpringEasing` | not supported | Written as CSS `ease`, an approximation |  |
| `Avalonia.Animation.EffectTransition` | not supported | Not written as a CSS transition |  |
| `Avalonia.Animation.FloatTransition` | not supported | Not written as a CSS transition |  |
| `Avalonia.Animation.IntegerTransition` | not supported | Not written as a CSS transition |  |
| `Avalonia.Animation.KeyFrame` | unmeasured |  | MOTION-01 |
| `Avalonia.Animation.PageSlide` | unmeasured | CSS enter animation | NAVIGATION-12 |
| `Avalonia.Animation.PointTransition` | not supported | Not written as a CSS transition |  |
| `Avalonia.Animation.RelativePointTransition` | not supported | Not written as a CSS transition |  |
| `Avalonia.Animation.Rotate3DTransition` | not supported | Written without an animation |  |
| `Avalonia.Animation.SizeTransition` | not supported | Not written as a CSS transition |  |
| `Avalonia.Animation.ThicknessTransition` | not supported | Not written as a CSS transition |  |
| `Avalonia.Animation.TransformOperationsTransition` | unmeasured | Render transform | MOTION-03 |
| `Avalonia.Animation.VectorTransition` | not supported | Not written as a CSS transition |  |
| `FillMode.Backward` | unmeasured |  | MOTION-01 |
| `FillMode.Both` | unmeasured |  | MOTION-01 |
| `FillMode.Forward` | unmeasured |  | MOTION-01 |
| `FillMode.None` | unmeasured |  | MOTION-01 |
| `PlaybackDirection.Alternate` | unmeasured |  | MOTION-01 |
| `PlaybackDirection.AlternateReverse` | unmeasured |  | MOTION-01 |
| `PlaybackDirection.Normal` | unmeasured |  | MOTION-01 |
| `PlaybackDirection.Reverse` | unmeasured |  | MOTION-01 |

## Markup extensions (`markup`)

Avalonia's markup extensions. XAML intrinsics (`x:Static`, `x:Type`, `x:Null`) are resolved by the XAML compiler before Aioros sees the controls.

| Construct | Status | Reason | Criteria and evidence |
| --- | --- | --- | --- |
| `Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindingExtension` | equal | See `CompiledBinding` | ENGINE-01, ENGINE-30 |
| `Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension` | unmeasured | Resolved by Avalonia per theme; one plan per theme | LAYOUT-17, CHARTS-06 |
| `Avalonia.Markup.Xaml.MarkupExtensions.OnFormFactorExtension` | not supported | Resolved for the server's form factor, not the visitor's |  |
| `Avalonia.Markup.Xaml.MarkupExtensions.OnPlatformExtension` | not supported | Resolved for the server's platform, not the visitor's |  |
| `Avalonia.Markup.Xaml.MarkupExtensions.ReflectionBindingExtension` | unmeasured | See `ReflectionBinding` | ENGINE-01 |
| `Avalonia.Markup.Xaml.MarkupExtensions.RelativeSourceExtension` | unmeasured |  | ENGINE-30 |
| `Avalonia.Markup.Xaml.MarkupExtensions.ResolveByNameExtension` | unmeasured | `#name` references: a property bound to the model is followed, one of an element that reads no model data is written as rendered | ENGINE-34, ENGINE-96 |
| `Avalonia.Markup.Xaml.MarkupExtensions.StaticResourceExtension` | unmeasured | Resolved by Avalonia when the XAML loads; no criterion yet |  |

## Brushes, effects and transforms (`visual`)

Brushes, effects and transforms.

| Construct | Status | Reason | Criteria and evidence |
| --- | --- | --- | --- |
| `Avalonia.Media.BlurEffect` | not supported | Effects are not written (would be `filter:blur()`) |  |
| `Avalonia.Media.ConicGradientBrush` | unmeasured | `conic-gradient`; on a shape's fill, the CSS gradient in a `<foreignObject>` clipped to the geometry (SVG has no conic paint); not on a stroke or in a self-drawn control, which stays a PNG | LAYOUT-21, LAYOUT-26, MEDIA-20 |
| `Avalonia.Media.DrawingBrush` | not supported | Not written |  |
| `Avalonia.Media.DropShadowDirectionEffect` | not supported | Effects are not written (would be `filter:drop-shadow()`) |  |
| `Avalonia.Media.DropShadowEffect` | not supported | Effects are not written (would be `filter:drop-shadow()`) |  |
| `Avalonia.Media.ImageBrush` | not supported | Not written (would be `background-image`) |  |
| `Avalonia.Media.Immutable.ImmutableConicGradientBrush` | unmeasured | `conic-gradient` | LAYOUT-21, MEDIA-20 |
| `Avalonia.Media.Immutable.ImmutableLinearGradientBrush` | equal | `linear-gradient` placed on the box in pixels; an SVG `<linearGradient>` on shapes and drawings | LAYOUT-21, LAYOUT-50, MEDIA-14, LAYOUT-53 |
| `Avalonia.Media.Immutable.ImmutableRadialGradientBrush` | equal | `radial-gradient`; an SVG `<radialGradient>` on shapes and drawings | LAYOUT-21, MEDIA-15, LAYOUT-53 |
| `Avalonia.Media.Immutable.ImmutableSolidColorBrush` | equal | Theme brushes; the same color as hex or `rgba()` | LAYOUT-17 |
| `Avalonia.Media.ImmutableBlurEffect` | not supported | Effects are not written (would be `filter:blur()`) |  |
| `Avalonia.Media.ImmutableDropShadowDirectionEffect` | not supported | Effects are not written (would be `filter:drop-shadow()`) |  |
| `Avalonia.Media.ImmutableDropShadowEffect` | not supported | Effects are not written (would be `filter:drop-shadow()`) |  |
| `Avalonia.Media.LinearGradientBrush` | equal | `linear-gradient` placed on the box in pixels as Avalonia places it (backgrounds of borders and panels, border brushes, reflected and repeated spreads); an SVG `<linearGradient>` in user space on Avalonia's targets for shapes and self-drawn controls | LAYOUT-21, LAYOUT-50, LAYOUT-51, LAYOUT-52, LAYOUT-53, MEDIA-14, MEDIA-17, MEDIA-18, bench:/controls/vectors |
| `Avalonia.Media.MatrixTransform` | equal | `transform:matrix()` with Avalonia's matrix | LAYOUT-12, ENGINE-37 |
| `Avalonia.Media.RadialGradientBrush` | equal | `radial-gradient` (its gradient origin is not expressible in CSS); an SVG `<radialGradient>` with its radii, origin, spread and transform for shapes and self-drawn controls | LAYOUT-21, LAYOUT-50, LAYOUT-53, MEDIA-15, MEDIA-17, bench:/controls/vectors |
| `Avalonia.Media.Rotate3DTransform` | not supported | Its perspective is lost in a 2D matrix |  |
| `Avalonia.Media.RotateTransform` | equal | `transform:matrix()` with Avalonia's matrix | LAYOUT-12 |
| `Avalonia.Media.ScaleTransform` | equal | `transform:matrix()` with Avalonia's matrix | LAYOUT-12 |
| `Avalonia.Media.SkewTransform` | equal | `transform:matrix()` with Avalonia's matrix | ENGINE-37 |
| `Avalonia.Media.SolidColorBrush` | equal | The same color as hex or `rgba()` | LAYOUT-04, LAYOUT-17 |
| `Avalonia.Media.TransformGroup` | equal | One matrix for the whole group | LAYOUT-12 |
| `Avalonia.Media.TranslateTransform` | equal | `transform:matrix()` with Avalonia's matrix | LAYOUT-12 |
| `Avalonia.Media.VisualBrush` | not supported | Not written |  |
