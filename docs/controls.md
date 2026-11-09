# Controls

Every public control type in Avalonia 12.1.3 (`Avalonia.Controls`), how Aioros writes it on the web, and the acceptance criteria that verify it. `tests/Aioros.ArchitectureTests` checks that every type is listed and that every criterion named here exists; a control is done when its criteria pass and its Control Gallery page matches Avalonia in the compatibility bench.

Status: `done`, or `n/a` with the reason. Every control is done; a `todo` row fails the build, so a control added to Avalonia must arrive with its criteria or its reason.

| Control | Family | Web | Status | Criteria |
| --- | --- | --- | --- | --- |
| `Avalonia.Controls.AutoCompleteBox` | Text | `<input>` with pattern, `type=number` or a `<datalist>` | done | TEXT-14, TEXT-15, TEXT-32 |
| `Avalonia.Controls.ColorPicker` | Text | `<input type=color>` (`#rrggbb`, the alpha kept on post), sized as its button; two-way bound on post (from `Avalonia.Controls.ColorPicker`) | done | TEXT-18 |
| `Avalonia.Controls.Label` | Text | Text element; `Label` is a `<label>` for its target | done | TEXT-07, TEXT-28, TEXT-33 |
| `Avalonia.Controls.MaskedTextBox` | Text | `<input>` with pattern, `type=number` or a `<datalist>` | done | TEXT-10, TEXT-11 |
| `Avalonia.Controls.NumericUpDown` | Text | `<input>` with pattern, `type=number` or a `<datalist>` | done | TEXT-12, TEXT-13 |
| `Avalonia.Controls.Primitives.AccessText` | Text | Text element; `Label` is a `<label>` for its target | done | TEXT-06 |
| `Avalonia.Controls.SelectableTextBlock` | Text | Text element; `Label` is a `<label>` for its target | done | TEXT-05 |
| `Avalonia.Controls.TextBlock` | Text | Text element (`div`, `h1`, `h2` by class); inlines as spans, a run or a span with `a:Web.Href` as an `<a href>` in its text; `MaxLines` as a line clamp | done | TEXT-01, TEXT-39, TEXT-40, TEXT-42 |
| `Avalonia.Controls.TextBox` | Text | `<input>` or `<textarea>` with its theme's border part (fill, border, shadow, states, transitions); `aria-invalid` with its messages; two-way bound on post | done | TEXT-02, TEXT-03, TEXT-34, TEXT-35, TEXT-36, TEXT-37, TEXT-38 |
| `Avalonia.Controls.Button` | Buttons | `<button type=submit>` running the command on post (a bound command, a method, or an MVVM framework's command action); unbound content takes its typed template | done | BUTTONS-01, BUTTONS-04, BUTTONS-05, BUTTONS-21, BUTTONS-78, BUTTONS-79, BUTTONS-80, BUTTONS-81, BUTTONS-83, BUTTONS-84, BUTTONS-85, ENGINE-63, ENGINE-64, BUTTONS-91, BUTTONS-95, BUTTONS-96, BUTTONS-99, BUTTONS-100, NAVIGATION-94, ENGINE-230, BUTTONS-111 |
| `Aioros.Avalonia.Controls.FilePicker` | Buttons | `<label>` with the button look around a hidden `<input type=file>` (`multiple` for `Files`, `accept` from `FileTypes`); posts as multipart and binds Avalonia's `IStorageFile`; on the desktop it opens `StorageProvider.OpenFilePickerAsync` (an Aioros control: Avalonia has no file-picking control) | done | BUTTONS-63, BUTTONS-64, BUTTONS-65, BUTTONS-66, BUTTONS-67 |
| `Avalonia.Controls.ButtonSpinner` | Buttons | Box with its content and increase and decrease submit buttons raising `Spin` on post | done | BUTTONS-14 |
| `Avalonia.Controls.CheckBox` | Buttons | `<input type=checkbox>` in a `<label>`, drawn with `appearance:none` as its theme draws the box and glyph; three-state as three cycling radios; a theme whose root is a named border is a card holding its parts, its indicator drawn by the input; its hot key a page shortcut | done | BUTTONS-03, BUTTONS-15, BUTTONS-86, BUTTONS-90, BUTTONS-115, BUTTONS-116 |
| `Avalonia.Controls.DropDownButton` | Buttons | `<button type=button>` with a chevron opening its flyout popover | done | BUTTONS-11 |
| `Avalonia.Controls.HyperlinkButton` | Buttons | `<a href>`, its control content written through its writers | done | BUTTONS-02, BUTTONS-20, BUTTONS-82, BUTTONS-112 |
| `Avalonia.Controls.Primitives.ToggleButton` | Buttons | `<label>` with a hidden checkbox and its theme's looks per class, checked under `:has(:checked)` | done | BUTTONS-07, BUTTONS-89 |
| `Avalonia.Controls.RadioButton` | Buttons | `<input type=radio>` in a `<label>`, one name per group, drawn as its theme draws the circle and dot; a theme whose root is a named border is a card holding its parts, its indicator drawn by the input; its hot key a page shortcut | done | BUTTONS-08, BUTTONS-09, BUTTONS-87, BUTTONS-114, BUTTONS-116 |
| `Avalonia.Controls.RepeatButton` | Buttons | `<button type=submit>` running the command once per press | done | BUTTONS-06 |
| `Avalonia.Controls.SplitButton` | Buttons | Submit button plus a chevron button opening its flyout popover | done | BUTTONS-12 |
| `Avalonia.Controls.ToggleSplitButton` | Buttons | Toggle label plus a chevron button opening its flyout popover | done | BUTTONS-13 |
| `Avalonia.Controls.ToggleSwitch` | Buttons | `<input type=checkbox role=switch>` drawn as its theme draws the track and knob (the knob placed and moved as Avalonia moves it) | done | BUTTONS-10, BUTTONS-88 |
| `Avalonia.Controls.Calendar` | Ranges | Fluent's: static month, year or decade table of the dates Avalonia computed; another theme's: written through its template, its navigation, caption and days posting their clicks, the shown month kept in a hidden field | done | RANGES-06, RANGES-07, RANGES-17, RANGES-18, RANGES-19, RANGES-20, RANGES-21, RANGES-22, RANGES-23 |
| `Avalonia.Controls.CalendarDatePicker` | Ranges | Fluent's: `<input type=date>` bound to `SelectedDate` on post; another theme's: written through its template, its drop-down button opening a popover with its calendar in the browser, a chosen day or typed text posted as Avalonia's picker takes them | done | RANGES-08, RANGES-09, RANGES-24, RANGES-25, RANGES-26, RANGES-27 |
| `Avalonia.Controls.DatePicker` | Ranges | `<input type=date>` bound to `SelectedDate` on post | done | RANGES-10, RANGES-11 |
| `Avalonia.Controls.DatePickerPresenter` | Ranges | Flyout of `DatePicker`; the browser's native date picker replaces it | n/a |  |
| `Avalonia.Controls.Primitives.CalendarButton` | Ranges | Part of `Calendar`: a month or year cell of its table, a posting button in its theme's look outside Fluent | done | RANGES-07, RANGES-22 |
| `Avalonia.Controls.Primitives.CalendarDayButton` | Ranges | Part of `Calendar`: a day cell of its table, a posting button in its theme's look of each state outside Fluent | done | RANGES-06, RANGES-18 |
| `Avalonia.Controls.Primitives.CalendarItem` | Ranges | Part of `Calendar`: its header and table, written through its template outside Fluent | done | RANGES-06, RANGES-07, RANGES-17 |
| `Avalonia.Controls.Primitives.DateTimePickerPanel` | Ranges | Scrolling column of the date and time flyouts; the browser's native pickers replace it | n/a |  |
| `Avalonia.Controls.Primitives.ScrollBar` | Ranges | Nothing: the browser draws the scroll bars | done | RANGES-14 |
| `Avalonia.Controls.Primitives.Thumb` | Ranges | Part of `Slider`: the range input's thumb, styled in CSS | done | RANGES-03, RANGES-16 |
| `Avalonia.Controls.Primitives.Track` | Ranges | Part of `Slider`: the range input's track and fill, styled in CSS | done | RANGES-03, RANGES-16 |
| `Avalonia.Controls.ProgressBar` | Ranges | `<progress>`, determinate or indeterminate, with its text | done | RANGES-04, RANGES-05 |
| `Avalonia.Controls.Slider` | Ranges | `<input type=range>` bound to `Value` on post, as tall as Avalonia lays it out, with its theme's track and thumb | done | RANGES-01, RANGES-02, RANGES-03, RANGES-16 |
| `Avalonia.Controls.TickBar` | Ranges | Part of `Slider`: ticks drawn as a CSS gradient across the track | done | RANGES-15 |
| `Avalonia.Controls.TimePicker` | Ranges | `<input type=time>` bound to `SelectedTime` on post | done | RANGES-12, RANGES-13 |
| `Avalonia.Controls.TimePickerPresenter` | Ranges | Flyout of `TimePicker`; the browser's native time picker replaces it | n/a |  |
| `Avalonia.Controls.Carousel` | Items | The WAI-ARIA carousel: scroll-snap slides along its `PageSlide`'s axis, each `ViewportFraction` of the strip, written through a template with previous, next and indicator parts; the carousel module moves it with its buttons, indicators, ArrowLeft and ArrowRight and a scroll by hand, wraps with `WrapSelection` and posts a bound selection | done | ITEMS-08, ITEMS-103, ITEMS-104, ITEMS-107, ITEMS-106 |
| `Avalonia.Controls.ComboBox` | Items | `<select>` drawn as its theme (glyph, states, placeholder option, disabled), options from the item template, `<hr>` separators, the theme's list as a customizable select; posts when a two-way selection changes | done | ITEMS-01, ITEMS-16, ITEMS-18, ITEMS-93, ITEMS-94, ITEMS-95, ITEMS-96, ITEMS-97 |
| `Avalonia.Controls.ComboBoxItem` | Items | Part of `ComboBox` (`<option>`) | done | ITEMS-01, ITEMS-96, ITEMS-97 |
| `Avalonia.Controls.ItemsControl` | Items | The items panel, with the item template as a repeat slot | done | ENGINE-04, LAYOUT-55 |
| `Avalonia.Controls.ListBox` | Items | Hidden radios (single) or checkboxes (multiple) in `<label>`s with the Fluent selected look; posts when a two-way selection changes | done | ITEMS-02, ITEMS-03, ITEMS-04, ITEMS-18, ITEMS-98, ITEMS-99, ITEMS-100 |
| `Avalonia.Controls.ListBoxItem` | Items | A `<label>` with a hidden radio or checkbox, selected look under `:has(:checked)` | done | ITEMS-02, ITEMS-03, ITEMS-04, ITEMS-99 |
| `Avalonia.Controls.PipsPager` | Items | Hidden radios as pips, previous and next as submit buttons | done | ITEMS-10 |
| `Avalonia.Controls.Primitives.HeaderedItemsControl` | Items | No theme in Fluent: generic template path | done | ITEMS-11 |
| `Avalonia.Controls.Primitives.HeaderedSelectingItemsControl` | Items | No theme in Fluent: generic template path | done | ITEMS-11 |
| `Avalonia.Controls.Primitives.SelectingItemsControl` | Items | No theme in Fluent: generic template path | done | ITEMS-11 |
| `Avalonia.Controls.Primitives.TabStrip` | Items | CSS-only tabs: hidden radios in tab `<label>`s; posts when a two-way selection changes | done | ITEMS-06, ITEMS-18 |
| `Avalonia.Controls.Primitives.TabStripItem` | Items | A tab `<label>` with a hidden radio and the Fluent pipe | done | ITEMS-06 |
| `Avalonia.Controls.TabControl` | Items | WAI-ARIA tabs: `role="tab"` buttons in a `role="tablist"` (in its theme's strip), `role="tabpanel"`s, switched by the keyboard module without a request | done | ITEMS-05, ITEMS-15, ITEMS-17, ITEMS-18, ITEMS-101, ITEMS-102, ITEMS-121 |
| `Avalonia.Controls.TabItem` | Items | A tab `<label>` and its `.tp` panel | done | ITEMS-05 |
| `Avalonia.Controls.TableView` | Items | `<table>` with `<colgroup>`, `<thead>` and a row repeat; a table of its own theme takes its headers' height and fill, the header line and its rows' borders by position from it; columns follow their bound visibility; a box marked `data-change` when its cells or headers bind fields two way | done | ITEMS-09, ITEMS-105, ITEMS-114, ITEMS-116, ITEMS-117, ITEMS-119, ITEMS-120 |
| `Avalonia.Controls.TableViewCell` | Items | `<td>` | done | ITEMS-09 |
| `Avalonia.Controls.TableViewColumnHeader` | Items | `<th>` holding its header as a cell holds its content (a control, a header template's content, a text), aligned as its content asks in a stretching column | done | ITEMS-09, ITEMS-113 |
| `Avalonia.Controls.TableViewRow` | Items | `<tr>` with its row look, per item when the look follows a flag the row binds two way | done | ITEMS-09, ITEMS-115 |
| `Avalonia.Controls.DataGrid` | Items | `<table>` with a `<colgroup>` of the visible columns' widths, a `<thead>` of their headers and a row repeat; all rows realized, not virtualized; a list its control template shows from its control (`{TemplateBinding}`) repeats too; grouped views and row headers are not written (from `Avalonia.Controls.DataGrid`) | done | ITEMS-91, ITEMS-92 |
| `Avalonia.Controls.DataGridRow` | Items | `<tr>` with the row's background, least height and cells' padding (from `Avalonia.Controls.DataGrid`) | done | ITEMS-91 |
| `Avalonia.Controls.DataGridCell` | Items | `<td>`: a text column's text, a template column's content as its template writes it (from `Avalonia.Controls.DataGrid`) | done | ITEMS-91 |
| `Avalonia.Controls.DataGridColumnHeader` | Items | `<th>` with the header's look (from `Avalonia.Controls.DataGrid`) | done | ITEMS-91 |
| `Avalonia.Controls.TreeView` | Items | Nested `<ul>` with `<details>`/`<summary>` | done | ITEMS-07 |
| `Avalonia.Controls.TreeViewItem` | Items | `<li>`: `<details open>` when expanded, a row for leaves | done | ITEMS-07 |
| `Avalonia.Controls.CarouselPage` | Navigation | A horizontal scroll-snap strip of pages starting at the selected one | done | NAVIGATION-19 |
| `Avalonia.Controls.CommandBar` | Navigation | `<menu>` toolbar; secondary commands in a "more" popover menu | done | NAVIGATION-10 |
| `Avalonia.Controls.CommandBarButton` | Navigation | Submitting `<button>` with icon and label | done | NAVIGATION-10 |
| `Avalonia.Controls.CommandBarSeparator` | Navigation | `<li role="separator">` in the toolbar, `<hr>` in the overflow menu | done | NAVIGATION-10 |
| `Avalonia.Controls.CommandBarToggleButton` | Navigation | `<label>` with a checkbox field | done | NAVIGATION-11 |
| `Avalonia.Controls.ContentPage` | Navigation | `<section>` with its top bar, content and bottom bar | done | NAVIGATION-15 |
| `Avalonia.Controls.ContextMenu` | Navigation | A menu popover the overlay module opens at the pointer on `contextmenu`, Shift+F10 or the menu key, with the look of the surface Avalonia shows it in; a context menu a control theme gives every such control is left to the browser | done | NAVIGATION-88, OVERLAY-05 |
| `Avalonia.Controls.DataValidationErrors` | Navigation | Error messages in `<div role="alert">` after the field, with its theme's error template's look, named by the field's `aria-describedby` | done | NAVIGATION-13, TEXT-37, TEXT-38 |
| `Avalonia.Controls.DrawerPage` | Navigation | `<aside popover>` drawer with a toggle button; docked as Avalonia resolves its display mode (`Split`, `CompactInline`, `Locked`), an overlay below `DrawerBreakpointLength` | done | NAVIGATION-18, NAVIGATION-106 |
| `Avalonia.Controls.Expander` | Navigation | `<details>`/`<summary>` | done | NAVIGATION-02, NAVIGATION-91, NAVIGATION-92, NAVIGATION-95 |
| `Avalonia.Controls.FlyoutPresenter` | Navigation | Anchored `<div popover role="dialog" class="fly">` with the presenter's look as Avalonia realizes it (theme, classes, box shadow, size, transitions) and its placement; attached flyouts open on hover or click | done | NAVIGATION-07, NAVIGATION-25, NAVIGATION-82, NAVIGATION-83, NAVIGATION-84, MOTION-09, OVERLAY-06, OVERLAY-07 |
| `Avalonia.Controls.Menu` | Navigation | `<menu role="menubar">` whose items open anchored `role="menu"` popovers, with the APG menubar keys; the bar's template shadow, items alignment and spacing, its items' looks (their `:open` look while their menu is open) and their menus' surfaces from their themes | done | NAVIGATION-03, NAVIGATION-06, NAVIGATION-93, OVERLAY-04 |
| `Avalonia.Controls.MenuFlyoutPresenter` | Navigation | Anchored `<div popover role="menu" class="menu">` with the surface's look and the APG menu keys, typeahead and pointer focus | done | NAVIGATION-04, NAVIGATION-24, NAVIGATION-86, OVERLAY-02, OVERLAY-03 |
| `Avalonia.Controls.MenuItem` | Navigation | `menuitem`, `menuitemcheckbox` or `menuitemradio` with its theme's look and layout: a submitting `<button>`, an `<a>` link, a toggle posting its click, or a button opening a nested menu; icon, gesture, the theme's check and dot, separators, labels | done | NAVIGATION-04, NAVIGATION-05, NAVIGATION-06, NAVIGATION-86, NAVIGATION-87 |
| `Avalonia.Controls.NavigationPage` | Navigation | `<header>` bar with the current page title and a back link (`a:Web.Href`); the stack is the browser history | done | NAVIGATION-16 |
| `Avalonia.Controls.Notifications.NotificationCard` | Navigation | Its control theme as Avalonia shows it at rest | done | NAVIGATION-09, MOTION-08 |
| `Avalonia.Controls.Notifications.WindowNotificationManager` | Navigation | The cards a post showed, written after the page in a manual popover at the panel's corner with its margins, stacked or arranged as the panel does, each fading after its expiration; their buttons post their command | done | ENGINE-147, ENGINE-216, SSR-155, NAVIGATION-89 |
| `Avalonia.Controls.PageNavigationHost` | Navigation | Its page in its place | done | NAVIGATION-20 |
| `Avalonia.Controls.RefreshContainer` | Navigation | Its content only: pull to refresh is the browser's reload, which renders the page again | done | NAVIGATION-14 |
| `Avalonia.Controls.RefreshVisualizer` | Navigation | Pull-to-refresh indicator: the browser draws its own when the page is pulled to reload | n/a | |
| `Avalonia.Controls.SplitView` | Navigation | Inline pane, or a popover drawer below a breakpoint | done | NAVIGATION-01 |
| `Avalonia.Controls.TabbedPage` | Navigation | CSS-only tabs: radio buttons of one field, the checked one shows its page | done | NAVIGATION-17 |
| `Avalonia.Controls.ToolTip` | Navigation | `role="tooltip"` the control is `aria-describedby`, placed by `ToolTip.Placement`, with its theme's bubble and arrow, shown by CSS on hover and `:focus-visible`; Escape and the skip delay in the overlay module | done | NAVIGATION-08, NAVIGATION-85, OVERLAY-08 |
| `Avalonia.Controls.TransitioningContentControl` | Navigation | Content with a CSS enter animation from its `PageTransition` | done | NAVIGATION-12 |
| `Avalonia.Controls.Canvas` | Media | One `<svg>` at its size: each child where the canvas arranges it, shapes as their rendered geometry, other controls in a `<foreignObject>` | done | MEDIA-01, MEDIA-11, MEDIA-16, MEDIA-19 |
| `Avalonia.Controls.Image` | Media | `<img>` with a PNG data URI, or a lazy `<img src>` for a URL in AsyncImageLoader's `ImageLoader.Source`; `alt` from the automation name, `object-fit` from `Stretch`, filling its box when stretched over it | done | MEDIA-06, MEDIA-09, MEDIA-23 |
| `Avalonia.Controls.PathIcon` | Media | Inline `<svg>` filled with `currentColor` | done | MEDIA-03 |
| `Avalonia.Controls.Shapes.Arc` | Media | Inline `<svg>` path of its geometry laid out at its size | done | MEDIA-05, MEDIA-12 |
| `Avalonia.Controls.Shapes.Ellipse` | Media | A CSS box with `border-radius:50%`; an SVG `<ellipse>` inside a `Canvas` or with a size and a gradient | done | MEDIA-04, MEDIA-12, MEDIA-17, MEDIA-20 |
| `Avalonia.Controls.Shapes.Line` | Media | Inline `<svg>` path of its geometry with Avalonia's pen; SVG inside a `Canvas` | done | MEDIA-05, MEDIA-12, MEDIA-13 |
| `Avalonia.Controls.Shapes.Path` | Media | Inline `<svg>` path from the Skia geometry, view box from `Stretch`, gradient paints; its rendered geometry inside a `Canvas` | done | MEDIA-02, MEDIA-12, MEDIA-13, MEDIA-14, MEDIA-18 |
| `Avalonia.Controls.Shapes.Polygon` | Media | Inline `<svg>` path of its geometry; a `<polygon>` inside a `Canvas` | done | MEDIA-05, MEDIA-12 |
| `Avalonia.Controls.Shapes.Polyline` | Media | Inline `<svg>` path of its geometry; a `<polyline>` inside a `Canvas`, bound points followed | done | MEDIA-05, MEDIA-13, MEDIA-19 |
| `Avalonia.Controls.Shapes.Rectangle` | Media | A CSS box (fill, stroke as border, dashed or dotted by its dash array, radius); an SVG `<rect>` inside a `Canvas` or with a size and a gradient or dashes | done | MEDIA-04, MEDIA-11, MEDIA-14, MEDIA-17, MEDIA-22, LAYOUT-52 |
| `Avalonia.Controls.Shapes.Sector` | Media | Inline `<svg>` path of its geometry laid out at its size | done | MEDIA-05, MEDIA-12 |
| `Avalonia.Controls.Viewbox` | Media | `<svg>` view box scaling its child in a `<foreignObject>` | done | MEDIA-07, MEDIA-18 |
| `Avalonia.Controls.Border` | Layout | Flex and CSS grid, borders (gradient backgrounds and border brushes placed as Avalonia places them), scrolling | done | LAYOUT-04, LAYOUT-21, LAYOUT-22, LAYOUT-50, LAYOUT-51, LAYOUT-52, LAYOUT-58 |
| `Avalonia.Controls.ContentControl` | Layout | Flex and CSS grid, borders, scrolling | done | LAYOUT-06, LAYOUT-56 |
| `Avalonia.Controls.Control` | Layout | Its template; a self-drawn control is an inline SVG recorded from its `Render`, or a PNG of its Avalonia render when SVG cannot reproduce it | done | LAYOUT-20, LAYOUT-23, LAYOUT-25, LAYOUT-26 |
| `Avalonia.Controls.Decorator` | Layout | Flex and CSS grid, borders, scrolling | done | LAYOUT-06 |
| `Avalonia.Controls.DockPanel` | Layout | CSS grid with a track per docked child | done | LAYOUT-08 |
| `Avalonia.Controls.ExperimentalAcrylicBorder` | Layout | Material colors with `backdrop-filter:blur()` | done | LAYOUT-18 |
| `Avalonia.Controls.Grid` | Layout | Flex and CSS grid, borders, scrolling | done | LAYOUT-02, LAYOUT-55 |
| `Avalonia.Controls.GridSplitter` | Layout | The WAI-ARIA window splitter: a focusable `role="separator"` with its value and limits, holding its template's parts, resized by the split module with the pointer and the keys and kept across answers | done | LAYOUT-11, LAYOUT-59, LAYOUT-62, LAYOUT-61 |
| `Avalonia.Controls.GroupBox` | Layout | `<fieldset>` with a `<legend>` | done | LAYOUT-15 |
| `Avalonia.Controls.LayoutTransformControl` | Layout | A box of the transformed size with the child transformed inside | done | LAYOUT-13 |
| `Avalonia.Controls.Panel` | Layout | One-cell CSS grid overlaying its children | done | LAYOUT-07 |
| `Avalonia.Controls.Primitives.HeaderedContentControl` | Layout | Its Fluent template (header row above content) | done | LAYOUT-16 |
| `Avalonia.Controls.Primitives.TemplatedControl` | Layout | Any control: its template is rendered, its clipping parts with their radius | done | LAYOUT-20, LAYOUT-57 |
| `Avalonia.Controls.Primitives.UniformGrid` | Layout | Flex and CSS grid, borders, scrolling | done | LAYOUT-03 |
| `Avalonia.Controls.RelativePanel` | Layout | Absolute positioning with CSS anchors; anchors precede their dependents | done | LAYOUT-09 |
| `Avalonia.Controls.ReversibleStackPanel` | Layout | Flex with a reversed direction | done | LAYOUT-10 |
| `Avalonia.Controls.ScrollViewer` | Layout | Flex and CSS grid, borders, scrolling; a viewer whose own theme frames its content is one scrolling box with the frame and thin bars | done | LAYOUT-05, LAYOUT-60 |
| `Avalonia.Controls.Separator` | Layout | `<hr>` with the theme stroke | done | LAYOUT-14 |
| `Avalonia.Controls.StackPanel` | Layout | Flex and CSS grid, borders, scrolling | done | LAYOUT-01, LAYOUT-24 |
| `Avalonia.Controls.ThemeVariantScope` | Layout | Its subtree resolved in its variant, with `color-scheme` | done | LAYOUT-17 |
| `Avalonia.Controls.UserControl` | Layout | Flex and CSS grid, borders, scrolling | done | LAYOUT-06 |
| `Avalonia.Controls.VirtualizingCarouselPanel` | Layout | Written by its `Carousel` as scroll-snap slides | done | ITEMS-08 |
| `Avalonia.Controls.VirtualizingStackPanel` | Layout | Every item realized, as a flex stack | done | LAYOUT-19 |
| `Avalonia.Controls.WrapPanel` | Layout | Flex and CSS grid, borders, scrolling | done | LAYOUT-03 |
| `Avalonia.Controls.Embedding.EmbeddableControlRoot` | — | Native embedding | n/a | |
| `Avalonia.Controls.NativeControlHost` | — | Hosts a native OS control | n/a | |
| `Avalonia.Controls.NativeMenuBar` | — | Native OS menu bar | n/a | |
| `Avalonia.Controls.Presenters.ContentPresenter` | — | Template plumbing: its content is written in its place | n/a | |
| `Avalonia.Controls.Presenters.ItemsPresenter` | — | Template plumbing: its panel is written in its place | n/a | |
| `Avalonia.Controls.Presenters.ScrollContentPresenter` | — | Template plumbing of ScrollViewer | n/a | |
| `Avalonia.Controls.Presenters.TableViewCellsPresenter` | — | Template plumbing of TableView | n/a | |
| `Avalonia.Controls.Presenters.TableViewColumnHeadersPresenter` | — | Template plumbing of TableView | n/a | |
| `Avalonia.Controls.Presenters.TextPresenter` | — | Template plumbing of TextBox | n/a | |
| `Avalonia.Controls.Primitives.AdornerLayer` | — | Internal adorner layer | n/a | |
| `Avalonia.Controls.Primitives.OverlayLayer` | — | Internal overlay layer | n/a | |
| `Avalonia.Controls.Primitives.OverlayPopupHost` | — | Internal popup host | n/a | |
| `Avalonia.Controls.Primitives.Popup` | — | Rendered through the control that owns it (flyouts, menus, tool tips) | n/a | |
| `Avalonia.Controls.Primitives.PopupRoot` | — | Native popup window | n/a | |
| `Avalonia.Controls.Primitives.TextSelectionHandle` | — | Touch text selection handle | n/a | |
| `Avalonia.Controls.Primitives.TextSelectorLayer` | — | Touch text selection layer | n/a | |
| `Avalonia.Controls.Primitives.VisualLayerManager` | — | Internal layer manager | n/a | |
| `Avalonia.Controls.Window` | — | The host window; the page is its content | n/a | |
| `Avalonia.Controls.WindowBase` | — | Base of windows | n/a | |
