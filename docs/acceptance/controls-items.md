# Controls: items

Selection and collections (`src/Adapters/Aioros.Avalonia/Html/Families/ItemsFamily.cs`). `ItemsControl` is covered by.

Selection works without JavaScript: a list whose selection posts is a group of visually hidden radio inputs (single selection) or checkboxes (multiple selection) inside `<label>`s that carry the item's Fluent look, and the selected look and the selected tab's panel follow `:has(>input:checked)` in CSS, as in the ARIA radio group and the CSS-only tab patterns. All of a group's inputs share the list's field name and post the item index, so the selection binds back by index. A bound `ItemsSource` becomes one repeat in the plan: the item body is learned from the first item, and the checked or selected mark is evaluated per item against the bound `SelectedIndex` or `SelectedItem`.

78 criteria. Each has an identifier and exactly one test.

## Items controls

### ITEMS-01: Combo boxes are selects

- **Given** a `ComboBox` with items (values or `ComboBoxItem`s)
- **When** it is rendered
- **Then** it is a `<select>` with one `<option>` per item, the `ComboBoxItem`'s content as the option text, and the selected item marked
- **When** a post names another option
- **Then** that item is selected; with `ItemsSource` bound to a view-model list, the plan writes the options of the list it renders.

### ITEMS-02: List boxes are radio groups

- **Given** a `ListBox` with single selection
- **When** it is rendered
- **Then** it is a `role="radiogroup"` with one `<label>` per item holding a hidden radio input named after the list whose value is the item index, `checked` on the selected item, and the style sheet gives the label the `ListBoxItem` padding and the Fluent selected background under `:has(>input:checked)`
- **When** a post names another index
- **Then** that item becomes the bound `SelectedItem`.

### ITEMS-03: Multiple selection list boxes are checkboxes

- **Given** a `ListBox` with `SelectionMode="Multiple"`
- **When** it is rendered
- **Then** each item holds a checkbox named after the list, `checked` on every selected item
- **When** a post names two indices
- **Then** both items are selected, and when a post names none, then the selection is cleared.

### ITEMS-04: Bound list boxes become a repeat with selection

- **Given** a `ListBox` bound to a list and to a `SelectedItem`
- **When** the plan is rendered with a model holding three items and the third selected
- **Then** the plan is reusable and writes three labels with each item's values and only the third `checked`.

### ITEMS-05: Tab controls are radio tabs with panels

- **Given** a `TabControl` with tab items
- **When** it is rendered
- **Then** it is a `data-aioros-tabs` box holding a `role="tablist"` of `<button type="button" role="tab">`s, each with its header and `aria-selected`, the selected one with `tabindex="0"` and a hidden input naming the selected index (its field), the others `tabindex="-1"`, followed by one `role="tabpanel"` per tab with that tab's content (every tab's content is written), all but the selected one `hidden`, so the selected panel shows without the script; the style sheet draws the Fluent selected pipe under `[aria-selected="true"]`
- **When** a post names another index
- **Then** that tab is selected.

*Precedent: the WAI-ARIA Authoring Practices' tabs pattern.*

### ITEMS-06: Tab strips are radio tabs

- **Given** a `TabStrip` with items
- **When** it is rendered
- **Then** it is a `role="radiogroup"` row of tab `<label>`s with hidden radio inputs and the Fluent tab look, the selected item `checked`
- **When** a post names another index
- **Then** that item is selected.

### ITEMS-07: Tree views are nested details

- **Given** a `TreeView` with nested items
- **When** it is rendered
- **Then** it is a `<ul>` whose items with children are `<li><details>` with the header in the `<summary>`, `open` when expanded and the children in a nested `<ul>`, leaves are rows without `<details>`, each row is indented by its level, and the selected item's row carries the selected look.

### ITEMS-08: Carousels are scroll snap slides

- **Given** a Fluent `Carousel` with three items and `SelectedIndex="1"`
- **When** it is rendered
- **Then** it is a horizontal scroll-snap container marked `data-aioros-carousel` and `data-aioros-strip`, `role="region"` and `aria-roledescription="carousel"`, with every item a full-width slide (`role="group"`, `aria-roledescription="slide"`) and the selected slide the initial scroll target; the carousel module moves it (ITEMS-106).

*Precedent: the WAI-ARIA carousel pattern; shadcn's `Carousel` (`role="region"`, `aria-roledescription="carousel"`, its items `role="group"` and `aria-roledescription="slide"`).*

### ITEMS-09: Table views are tables

- **Given** a `TableView` with columns bound to a list
- **When** the plan is rendered with a model holding three items
- **Then** it is a `<table>` with one `<th>` per visible column header and one `<tr>` per item with a `<td>` per column holding the item's value, right-aligned columns aligned right.

### ITEMS-10: Pips pagers are radio pips with steps

- **Given** a `PipsPager` with a number of pages
- **When** it is rendered
- **Then** it is a row of one hidden radio input per page with the selected page `checked`, and previous and next submit buttons, the previous one hidden on the first page
- **When** a post names a page
- **Then** it becomes the selected page, and when the next button is posted, then the page after the posted one is selected.

### ITEMS-11: Items base types go through the generic path

- **Given** the base types `SelectingItemsControl`, `HeaderedItemsControl` and `HeaderedSelectingItemsControl` with items
- **When** they are rendered
- **Then** each goes through the generic template path and is written as an empty element without its items, as Avalonia, which has no theme for them, draws nothing in their place.

### ITEMS-12: Items written in XAML are planned

- **Given** an `ItemsControl` whose items are written in XAML (no bound `ItemsSource`)
- **When** the page is compiled
- **Then** the plan is reusable and writes those items as they are, each as a list item.

### ITEMS-13: Combo boxes are as tall as in Avalonia

- **Given** a Fluent `ComboBox` and one with `MinHeight="48"`
- **When** they are rendered
- **Then** each `<select>` gets the height Avalonia lays it out at: its padding and border plus its text line (Avalonia's line spacing rounded up, TEXT-16), at least its `MinHeight` (`height:32px` and `height:48px`), since the browser's own `<select>` keeps `line-height:normal` and would be a pixel taller.

### ITEMS-14: Selected items have their pressed look

- **Given** a `ListBox` with a selected item and a `TreeView` with a selected item
- **When** they are rendered
- **Then** each item's look has a rule for selected and pressed (`:checked` or `.sel` with `:active`) carrying what Avalonia paints with `:selected`, `:pointerover` and `:pressed` set (the `ListBoxItem`'s accent fill, the `TreeViewItem`'s pressed fill), different from the selected and pointer-over rule, instead of keeping the selected hover look while pressed.

### ITEMS-15: Bound tab headers take the look of their own selection

- **Given** a `TabControl` whose `ItemsSource` and `SelectedItem` are bound, with an `ItemTemplate` for the headers
- **When** the plan is rendered with a model holding three items and the second selected
- **Then** the plan is reusable (it renders what Avalonia renders), every header is written with the same class and no color of its own, so it takes the color of its tab's look, which differs between the unselected tab (Avalonia's dimmed header) and the checked one, and only the second tab is `aria-selected="true"`, its panel the only one shown.

### ITEMS-16: Combo box options are keyed by their index

- **Given** a `ComboBox` whose `ItemsSource` is bound to objects that all have the same text and whose `SelectedItem` is bound two-way
- **When** the plan is rendered with three items and the second selected
- **Then** each `<option>` carries its index in the list as its `value` and only the second is `selected`, the selected item being matched as Avalonia matches it (the same object, or an equal one) rather than by its text
- **When** a post names an index
- **Then** the item at that index becomes the selected item, through Avalonia and through the plan alike.

### ITEMS-17: Bound tab panels take the template of their item type

- **Given** a `TabControl` whose `ItemsSource` is bound to items of different types, whose `ContentTemplate` holds a `ContentControl` showing the item and whose typed `DataTemplate`s pick each type's look, one of them a templated `UserControl` (as a conductor of screens shows each screen's view)
- **When** the plan is rendered with other items
- **Then** the plan is reusable and every tab's panel holds its own item's template, the same bytes Avalonia renders for those items, unselected tabs included; a `TabControl` bound to items without a `ContentTemplate` likewise shows each item's own text in its panel.

### ITEMS-18: Two way selections are marked to post when they change

- **Given** a `ListBox`, a `TabControl` and a `ComboBox` whose selection is bound two-way, a `TabStrip` bound one way and a `ListBox` with no bound selection
- **When** they are rendered
- **Then** the first three carry `data-change` on their group, tab list or `<select>`, which the framework's script reads to post the selection when it changes in the browser, and the other two do not, since nothing in the model follows their selection; the plan is reusable.

### ITEMS-28: Container themes style tabs before their templates

- **Given** a `TabControl` whose `ControlTheme` sets an `ItemContainerTheme` (as Semi's `LineTabControl` does) and whose `TabItem`s declare their content inline, one of them holding another such `TabControl` (written later, as an unselected tab's panel)
- **When** it is rendered
- **Then** each tab is styled by that container theme before its template is applied, as Avalonia styles a control before templating it in a measure pass, so the selected tab's content is shown once in the tab panel and the page renders instead of answering 500 because one control was given to two template presenters.

### ITEMS-49: Static lists of objects are combo box options

- **Given** a `ComboBox` whose `ItemsSource` is an `x:Static` list of objects (a public static array on the items' own type, as AdvancedToDoList's `ToDoItemsSortExpression.AvailableSortExpressions`) and whose `SelectedItem` is bound two-way
- **When** the page is compiled
- **Then** the plan is reusable and renders, also after the plans file, the options and the selected one as Avalonia renders them for any model
- **When** a post names an option's index
- **Then**
  - The model's property becomes the list's object at that index (the same object), through the plan as through Avalonia.
  - The plan names the list by its static member (`static:Type:Member`), found among the public static members of the items' types, and the runtime reads it through the app's generated code; a list that no such member holds keeps the field undescribed, so its posts go through the Avalonia engine.

### ITEMS-70: Bound tree views are planned to any depth

- **Given** `TreeView`s whose `ItemsSource` is bound, one with a `TreeDataTemplate` whose children are items of the same type (as Semi's demo nests `TreeViewItemVm.Items`) and one with a typed `TreeDataTemplate` per level (regions, their cities, their streets)
- **When** the page is compiled and rendered for other trees (deeper than the compiled one, with a branch left without children, a former leaf given children, empty)
- **Then** the plan is reusable and each render, also after the plans file, writes the trees as Avalonia's render does: each item a leaf row or a `<details>` branch with its children in a nested list, to any depth, each row indented by its level
- **When** a bound tree whose item is selected
- **Then** the plan is not reusable and the reason names the selection.

*Precedent: Avalonia's `TreeDataTemplate`, which gives each item its children's binding, and `TreeViewItem.Level`.*

### ITEMS-91: Data grids are tables

- **Given** a `DataGrid` (from `Avalonia.Controls.DataGrid`) bound to a list, with a text column, a template column whose cell template shows a member, and a hidden column
- **When** the plan is rendered with a model holding three items
- **Then** it is a `<table>` with a `<colgroup>` of the visible columns' widths (pixels as pixels, stars as their share of what pixels leave), a `<thead>` with one `<th>` per visible column header and a `<tbody>` with one `<tr>` per item holding a `<td>` per visible column (a text column's value as text, a template column's cell as its template writes it), byte for byte as Avalonia's render of lists the compiled page never held; a grid bound to a list that is empty at compile fails the plan, since no row can be learned from it.

### ITEMS-92: Template data grids repeat their controls list

- **Given** a `DataGrid` in a control template whose `ItemsSource` shows its templated control's list (`ItemsSource="{TemplateBinding Rows}"`, as Semi's palette lists its functional colors and shadows), the control's list bound to the model
- **When** the page is planned
- **Then** the plan is reusable and writes one row per item of the model's list, byte for byte as Avalonia's render of lists the compiled page never held.

### ITEMS-98: Bound selected items are planned

- **Given** a multiple-selection `ListBox` whose `ItemsSource` and `SelectedItems` are bound (a model's list of styles and its collection of chosen ones, as Aioros.Cosmo's multiple toggle group binds them)
- **When** the page is planned and rendered for two models
- **Then** the plan is reusable and checks the items the model's collection holds, as Avalonia's selection model shows them, for every model and after the plans file; the list's field is not described, so its posts go through the engine, which writes the selection into the bound collection as Avalonia does.

### ITEMS-99: List items take their themes box and name

- **Given** a multiple-selection `ListBox` whose item container theme gives its items a 1 px border (`#e5e5e5`, `#171717` when selected), a 6 px radius, a 36 px height, a medium weight and centered content (Aioros.Cosmo's outline toggle group), one item an icon named `Bold` through `AutomationProperties.Name`
- **When** the page is rendered
- **Then** each item's `<label>` has that height, weight, border and radius with its content centered both ways, the selected look's border color under `:has(>input:checked)`, and the named item holds its name as visually hidden text, which names its checkbox for assistive technologies.

### ITEMS-100: List items take the look of their position

- **Given** `ListBox`es whose styles round their first item's left corners (`ListBoxItem:nth-child(1)`, 8 px), their last item's right corners (`:nth-last-child(1)`) and an only item's four (both), as Aioros.Cosmo's toggle group, one with items written in the XAML and one bound to the model's list
- **When** the page is compiled from a list of three items and rendered for lists of one, two, three and five
- **Then**
  - The plan is reusable and writes Avalonia's render for each, and each item's `<label>` has the corners Avalonia gives the item at its position: `8px 0px 0px 8px` first, none in the middle, `0px 8px 8px 0px` last, `8px` alone; the plan learns the label's look first, middle, last and only from stand-in lists of the traced item (ENGINE-217), and a list whose items look alike keeps one label for every item.
  - A bound list whose `SelectedItem` is bound two way (Aioros.Cosmo's toggle group of alignments) keeps the model's selection while the plan learns its positions from stand-in lists, so its plan writes the selected item `checked` as Avalonia does.

### ITEMS-101: Tabs sit in their themes strip

- **Given** a `TabControl` whose control theme wraps its items presenter in a `PART_TabsList` border (`#f5f5f5`, 10 px corners, 3 px padding, 36 px high), as Aioros.Cosmo's tabs, and whose `TabItem` theme gives its `PART_LayoutRoot` a 1 px transparent border, a white background, a `#e5e5e5` border and a shadow when selected and an opacity of 0.5 when disabled, with three tabs written in the XAML, the third disabled
- **When** the page is rendered
- **Then** the `role="tablist"` (ITEMS-05) has the strip's box (background, radius, padding, height) and the panels follow it in order; each tab has the root's 1 px solid border and, selected (`[aria-selected="true"]`), its background, border color and shadow; the disabled tab has its opacity and is a `disabled` button; a tab control whose items presenter is not inside such a border (Fluent's) has a tab list without a box.

### ITEMS-102: Bound tabs sit in their themes strip

- **Given** a `TabControl` of the theme of ITEMS-101 whose `ItemsSource` and `SelectedIndex` are bound to the model, with an `ItemTemplate` for the headers and a `ContentTemplate`
- **When** the page is compiled and rendered for lists of one, two and four items with another index selected
- **Then** the plan is reusable and writes Avalonia's render for each, the tabs inside the strip's box, the selected tab and its panel the only ones selected and shown, and the tab list carries `data-change`, so the script posts a selection made in the browser.

### ITEMS-105: A tables theme gives its header and rows their look

- **Given** a `TableView` whose control themes are not Fluent's, as Aioros.Cosmo's table: a `TableViewColumnHeader` 40 px high whose content presenter has no background, a scroll viewer theme whose `PART_Header` border draws a 1 px `#e5e5e5` line under the headers, and a `TableViewRow` whose template border has a 1 px `#e5e5e5` bottom border but on the last row (`:nth-last-child(1)`), bound to the model, and a Fluent `TableView`
- **When** the page is rendered for three rows and planned from one row (the stand-ins giving the positions)
- **Then** the themed table's headers are 40 px high with a transparent background over a `1px solid #e5e5e5` line, each row but the last has a `1px solid #e5e5e5` bottom border and the last none, as Avalonia draws them; the Fluent table keeps its chrome header background and no row borders.

### ITEMS-121: An empty panel whose host hides is out of the layout

- **Given** a `TabControl` whose template stacks its strip and its content host with an 8 px spacing and hides the host while the selected content is null (`IsVisible="{TemplateBinding SelectedContent, Converter={x:Static ObjectConverters.IsNotNull}}"`), its tabs without content (Aioros.Cosmo's `tabs-list`, shadcn/ui's `TabsList` alone), and one whose host stays visible
- **When** the pages are rendered
- **Then** the first's panels that hold nothing are out of the layout (`:empty{display:none}` on its panels' class), so its box is the strip's 36 px as Avalonia lays it out and no gap follows the strip, while a panel with content is shown as before; the second keeps its empty panel in the layout and the gap before it (44 px), as Avalonia's stack panel spaces a visible empty presenter.

### ITEMS-147: Combo boxes with a bound index are answered from the plan

- **Given** a `ComboBox` of three `ComboBoxItem`s whose `SelectedIndex` is bound two way to the model's number
- **When** the page is compiled for the web
- **Then** the plan is reusable and describes the field (a page with it is still answered from its plan), it writes the option at the model's index selected (the third for a model at `2`) as Avalonia does, also when read back from the plans file, and a post of `1` through the plan sets the model's number to `1`

*Precedent: ITEMS' `ListBox` with a bound `SelectedIndex` (`choice-index`); the HTML `<select>` posting its option's value.*

## Hierarchy plan

### ITEMS-80: Menus of menus are learned per level and look

- **Given** a templated list whose item containers are lists of their own (as Ursa's `NavMenu` and `NavMenuItem`: each container binds its header and its children through bindings the menu holds, takes its `Level` from its place and its template from the item, a separator template set by a style binding `IsSeparator`, a chevron hidden by `:empty`, a style binding of `IsVisible`, a padding from a `MultiBinding` of `Level`, and a header that reads the container's own state no binding sets, as Ursa's icons read `NavMenuItem.IsHighlighted`)
- **When** the page is planned with a model of separators, leaves and a branch
- **Then** the plan is reusable, learns one body per level and per look (each style-bound `bool` true or false, with children or without, each set above its binding when the model has no such item; the container's state that no binding sets, such as its level, is part of the look), and writes, byte for byte as Avalonia's render, models whose items have other names, another arrangement of looks (a hidden item, a separator inside a branch), or no items; a style binding that writes to its source leaves the compiled model as it was.

## Plan reach

### ITEMS-81: Null lists learn from their declared items

- **Given** an `ItemsControl` whose `ItemsSource` is bound to a member that is null when the page is compiled, declared as a list of a type (`string[]? Tags`, as Ursa's page metadata lists its tags), or held by a data context bound to a member that is null (`DataContext="{Binding Inner}"`, as Ursa's layout places the page metadata of pages that have none), and whose item template names no data type
- **When** the page is planned
- **Then** the plan is reusable, learns the item template from a stand-in of the declared element type, and writes nothing for a null list and each item for a filled one, byte for byte as Avalonia's render.

## Select theme

### ITEMS-93: Selects are drawn as their theme

- **Given** a `ComboBox` whose control theme draws its `Background` border (transparent, `#e5e5e5`, radius 6, a `BoxShadow`), a 16 px stroked `DropDownGlyph` path 12 px from the right edge, a `:pointerover` fill `#f5f5f5` and a `:focus-visible` border and ring (Aioros.Cosmo's)
- **When** the page is rendered
- **Then** its `<select>` is drawn with `appearance:none`: the border, radius, transparent fill and shadow at rest, the glyph as an SVG background from the template's own path at `right 12px center`, a right padding that keeps the text out of the glyph's column as Avalonia's grid does (35 px inside the 1 px border), and `:hover` (`background-color`, so the glyph stays) and `:focus-visible` rules with the theme's looks, instead of the browser's own field and arrow.

### ITEMS-94: Selects write their placeholder and disabled state

- **Given** a `ComboBox` with a `PlaceholderText` whose `SelectedItem` is bound, and a disabled `ComboBox`
- **When** the page is planned and rendered for a model with no selection and one with a selection
- **Then** the plan is reusable and writes what Avalonia renders for both: a first option with an empty value, `disabled` and `hidden`, holding the placeholder and `selected` while nothing is (so a post never picks the first item), colored as the theme's `PlaceholderForeground` while chosen; and the disabled combo's `<select>` is `disabled`.

### ITEMS-95: Options show what their templates write

- **Given** a `ComboBox` whose items are bound model objects shown by an `ItemTemplate` whose one text binds `Name`, and one with `DisplayMemberBinding="{Binding Name}"`
- **When** the page is planned and rendered for two models
- **Then** the plan is reusable and each option holds the item's name, as Avalonia shows it, for every model, instead of the object's type name.

### ITEMS-96: Disabled items and separators are written as such

- **Given** a `ComboBox` whose items are `ComboBoxItem`s, one disabled with a text (a header), one disabled with no content (a separator) and one holding a `TextBlock`
- **When** the page is rendered
- **Then** the disabled one with a text is a `disabled` option (Avalonia selects no disabled item; it keeps its index as value), the empty one is an `<hr>`, and the `TextBlock`'s item shows its text.

### ITEMS-97: Customizable select lists take the themes popup

- **Given** the `ComboBox` of ITEMS-93 whose popup's `PopupBorder` is white with a border, radius, shadow and 4 px around its items, and whose `ComboBoxItem` theme gives padding `8,6,32,6`, radius 4, a 20 px line and a `:pointerover` fill
- **When** the page is rendered
- **Then** under `@supports (appearance:base-select)` the select and its `::picker(select)` take `appearance:base-select`, the browser's `::picker-icon` is hidden (the theme's glyph is drawn), the picker carries the popup border's fill, border, radius, padding and shadow, and its options the item's padding, radius, line height and their `:hover` look, so Chromium draws the theme's list while Firefox keeps its own.

### ITEMS-134: Scroll buttons stand at the lists edges while it can scroll

- **Given** a `ComboBox` whose popup template holds, around its list's scroll viewer, controls marked `a:Web.ScrollButton` `Up` and `Down` (Aioros.Cosmo's `c:SelectScrollUpButton` and `c:SelectScrollDownButton`)
- **When** it is rendered
- **Then** where the browser has the customizable select its `<select>` holds before its options and after them an `aria-hidden` element drawn as each control (its padding and its drawing, also when the control starts hidden, as Aioros.Cosmo's scroll-up button does at the list's start), sticky at the top and the bottom of the picker and as wide as it inside its border (over its padding, as the controls stand outside the list's margin), shown only while the picker can scroll that way (scroll-state container queries on `::picker(select)`), with the picker's scroll padding the buttons' height and its height at most the popup's `MaxHeight`; without the hint nothing is written, and where the browser has no customizable select the elements show nothing.

*Precedent: Radix Select's `SelectScrollUpButton` and `SelectScrollDownButton`, CSS Conditional Rules 5's `scroll-state` queries, HTML's customizable select content model.*

### ITEMS-135: Scroll buttons scroll the list while pointed at

- **Given** that `ComboBox` in Chromium with the framework's script, its picker open at its start
- **When** the pointer rests on the scroll-down element
- **Then** every 50 ms the picker scrolls by the selected option's height (the first enabled option's when none is selected, the selected one being the hidden placeholder of a `ComboBox` with `PlaceholderText` and no selection), the scroll-up element shows once the picker has left its start, and the scrolling stops when the pointer leaves the element or the picker reaches its end; the scroll-up element scrolls back the same way, as Radix Select's scroll buttons do.

### ITEMS-136: The customizable select outweighs the fields own class

- **Given** the `ComboBox` of ITEMS-97 in Chromium, whose own class draws its closed field with `appearance:none`
- **When** the browser computes it and opens its picker
- **Then** the select computes `appearance:base-select` (the customizable select's rule outweighs the field's own class, whatever their order in the style sheet) and its open picker shows the popup border's fill, so the theme's list is what the visitor sees; before, the field's class came later and won, and the browser's own list showed.

### ITEMS-137: The picker stands where the popup is placed

- **Given** a `ComboBox` whose popup is placed `Bottom` with a vertical offset of 4 around a popup border with an 8 px margin, the popup's child (Aioros.Cosmo's select)
- **When** its customizable select's picker opens in Chromium
- **Then**
  - The picker stands as Avalonia's popup positioner places that border: centered under the field (`position-area:bottom`), 4 px below it, flipped above it when it would leave the view, through the placement every anchored popover takes (`Placements`); the child's margin moves nothing, as Avalonia 12's positioner places the popup's size deflated by its child's margin (room for its shadow), so an offset of -4 makes the border overlap the field by 4 px on both targets, while the margin of a border nested inside the popup's child still moves it.
  - Restated on 2026-10-22: the margin used to be added to the offset, 8 px off the desktop.

*Precedent: Avalonia 12.1.3's `PopupPositionerExtensions.Update` and `ManagedPopupPositioner.Update` (`Size.Deflate(Deflate)`, the child's margin from `OverlayPopupHost` and `PopupRoot`, read with ilspycmd).*

### ITEMS-138: Disabled options take the disabled items look

- **Given** a `ComboBox` holding disabled items drawn with a look of their own (Aioros.Cosmo's `label` class: 12 px on a 16 px line, its muted foreground, opacity 1, padding 8,6)
- **When** its customizable select's picker opens in Chromium
- **Then** its disabled options take that look instead of the enabled items' and the browser's disabled color: their font size, line height, padding, color and opacity are the disabled item's as its theme draws it; a list without disabled items writes no such rule.

### ITEMS-139: The check mark is the themes selected part

- **Given** a `ComboBoxItem` theme whose template shows a part only while the item is selected (Aioros.Cosmo's 16 px check at 7 px from the right, over the item's 32 px right padding)
- **When** the customizable select's picker opens in Chromium
- **Then** the selected option's `::checkmark` is that part drawn as an SVG at its size where the template places it (also when the popup was laid out before the item was selected, as its scroll buttons are), over the option's padding (so the list is as wide as its widest item, as on the desktop), and an item theme with no such part (Fluent's) shows no check mark of the browser's own.

### ITEMS-140: A native list keeps the browsers select

- **Given** a `ComboBox` drawn by a theme with a popup, marked `a:Web.NativeList` (Aioros.Cosmo's `native` class, shadcn/ui's `NativeSelect`, a native `<select>` with the browser's own list), and the same `ComboBox` unmarked
- **When** they are rendered and Chromium computes them
- **Then** the marked select writes no customizable select rule, computes `appearance:none` and is as wide as its widest option, as a native select is, while the unmarked one keeps the customizable select (ITEMS-136).

### ITEMS-141: An opening list scrolls its selected option the nearest way

- **Given** that `ComboBox` (ITEMS-134) with its ninth of twelve options selected, below the picker's first screen, in Chromium with the framework's script
- **When** its picker opens
- **Then** the selected option is scrolled into view the nearest way from the picker's start, as Radix Select focuses its selected item (`scrollIntoView({ block: 'nearest' })`): the option's bottom meets the scroll-down element's top and the scroll-up element shows, instead of the browser centering the focused option; without scroll buttons the browser's own scrolling is kept.

### ITEMS-142: The picker keeps its collision padding from the viewports edges

- **Given** a `ComboBox` placed `Bottom` whose popup carries `a:Web.CollisionPadding="10"` (Aioros.Cosmo's select: Radix Select keeps its list 10 px from the viewport's edges, its `collisionPadding`; Avalonia's positioner has no such distance, Aioros.Cosmo keeps it on the desktop), its list wider than the field
- **When** its customizable select's picker opens in Chromium with the field 2 px from the viewport's left edge, and again 2 px from its right edge
- **Then** the picker's border stands 10 px from that edge instead of touching it, shifted along the field as little as that takes (Radix Popper's `shift` with `padding`), written as the insets of the edges its `position-area` shares with the viewport across its side; without the hint the picker still reaches the edge.

*Precedent: Radix Popper's `shift({ padding: collisionPadding })`; CSS Anchor Positioning's inset-modified containing block for `position-area`.*

### ITEMS-143: A disabled select takes its parts disabled colors

- **Given** a `ComboBox` theme whose `:disabled` style colors its template's `ContentPresenter` and `PlaceholderTextBlock` parts (Semi.Avalonia's `ComboBox`: its disabled text in `SemiColorDisabledText`)
- **When** a disabled `ComboBox` with a selected item, and one showing its placeholder, are written
- **Then** in Chromium the select's text and its placeholder compute those colors, read from the parts in the control's state as Avalonia draws them, and an enabled one keeps its own.

## Carousel

### ITEMS-103: Carousels are written through their template

- **Given** a `Carousel` whose control theme lays a `PART_ScrollViewer` beside `PART_Previous` and `PART_Next` buttons and a `PART_Indicators` panel of one button per slide (as Aioros.Cosmo's theme), with `WrapSelection="True"`
- **When** it is rendered
- **Then** it is written through its template: the scroll-snap strip (`data-aioros-strip`) where the template's scroll viewer is, the previous and next buttons as `<button type="button" data-aioros-slide="-1">` and `data-aioros-slide="1"` with their look and content, the indicators as one `<button type="button" data-aioros-slide-to>` per slide, the selected one with its own look and `aria-current="true"`, and the carousel marked `data-aioros-wrap`; a carousel whose `PageTransition` is a vertical `PageSlide` is `data-aioros-carousel="v"` with a vertical strip; a `ViewportFraction` of 0.5 makes each slide half the strip, snapped at its center; a Fluent carousel writes no buttons.

*Precedent: Avalonia's `Carousel` (`WrapSelection`, `ViewportFraction`, `PageSlide.Orientation`); shadcn's `CarouselPrevious`, `CarouselNext` and Embla's `loop`, `axis` and `align`.*

### ITEMS-104: Bound carousels post their selected slide

- **Given** a `Carousel` whose `ItemsSource` and `SelectedIndex` are bound two way, in the theme of ITEMS-103
- **When** it is rendered for models of two and three items
- **Then** the plan writes what Avalonia renders: one slide and one indicator per item, the selected slide holding the hidden field that names its index and the carousel marked `data-change`
- **When** a post names index 2
- **Then** the model's index is 2, through the plan and through the engine.

*Precedent: Phoenix LiveView's `phx-change`; the tabs of ITEMS-102.*

### ITEMS-107: Aspect ratios keep their slides height

- **Given** a control with `a:Web.AspectRatio="2"` (a library panel that keeps a ratio in its own layout, as Aioros.Cosmo's `c:AspectRatio`) inside a carousel's slide
- **When** it is rendered
- **Then** its element carries `aspect-ratio:2`, so the slide takes its height from its width rather than collapsing to its content.

*Precedent: CSS `aspect-ratio`, as shadcn's `AspectRatio` (Radix) and its carousel's `aspect-square` cards.*

### ITEMS-118: Vertical strips take their laid out height

- **Given** a vertical `Carousel` 196 px high whose template lays its `PART_ScrollViewer` between its 24 px previous and next buttons (as Aioros.Cosmo's `carousel-orientation`), with `ViewportFraction="0.7449"` and slides 400 px high, and a horizontal one
- **When** they are rendered
- **Then** the vertical strip and every part between it and the carousel fill the carousel's height (class `csf`: `min-height:0`, a flexible basis in a column), as Avalonia hands a template's parts the carousel's bounded height, so each slide is 74.49% of the strip rather than the strip growing with its slides; the horizontal carousel's parts are not marked.

### ITEMS-145: A swipeable carousel is dragged by the mouse

- **Given** a `Carousel` with `IsSwipeEnabled` and one without it
- **When** the page is rendered
- **Then** the first carousel is marked `data-aioros-drag` and the second is not.

## Carousel script

### ITEMS-106: Carousels move with their buttons indicators keys and scroll

- **Given** carousels marked as ITEMS-103 writes them, one without wrapping and with a bound selection (`data-change`) and one wrapping
- **When** the next button is clicked
- **Then**
  - The strip scrolls to the next slide, which becomes the selected one (`sel`), the hidden field moves into it with its index and the change is posted; at the last slide the next button is disabled and at the first the previous one, unless the carousel wraps, where next on the last selects the first; an indicator selects its slide and takes the selected look and `aria-current` from the one that had them
  - ArrowRight and ArrowLeft with the focus inside the carousel move as the buttons do; scrolling the strip to a slide by hand selects it once the scroll ends.

*Precedent: shadcn's `Carousel` (Embla's `scrollPrev`, `scrollNext`, `canScrollPrev` and `canScrollNext`, ArrowLeft and ArrowRight in `onKeyDownCapture`); the WAI-ARIA carousel pattern.*

### ITEMS-146: The mouse drags a swipeable carousel as embla does

- **Given** a carousel marked `data-aioros-drag` of four 200 px slides in Chromium on a controlled clock
- **When** the mouse drags its strip 80 px left, holds it 200 ms (so the drag is no flick) and lets go
- **Then** the strip followed the pointer and settles on the slide nearest where it stands (the first); dragged 95 px and still moving, but slower than 0.1 px/ms over its last 170 ms, it settles on the first too; dragged 60 px left and let go at once (a flick), it goes to the next slide, though the first is nearer; dragged right from the second slide in a flick, it goes back to the first; a drag past 10 px does not click the slide's button, a press without a drag does; and a carousel without the mark is not dragged by the mouse.

*Precedent: Embla Carousel 8.5.2's `DragHandler` (`dragThreshold` 10, `goToNextThreshold` a fifth of the view between 50 and 225 px, the mouse's force boost 300) and `DragTracker` (a flick over 0.1 px/ms in its last 170 ms).*

## Filtered list

### ITEMS-110: Filtered lists are comboboxes over a listbox

- **Given** a library list whose control template marks its text box `a:Web.Filter="Input"` and its empty part `a:Web.Filter="Empty"` (`Finder`, shaped as Aioros.Cosmo's `c:Command` with its items panel, groups with headers, a separator, a disabled item, an item with a shortcut and an item whose `TextSearch.Text` is set, its first item `:selected` by the library's code as the page is written)
- **When** the page is compiled
- **Then** the plan is reusable and the input carries `data-aioros-filter`, `role="combobox"`, `aria-autocomplete="list"` and `aria-expanded="true"`; the items panel is the `listbox`, each group a `group`, each item an `option` (`data-value` from `TextSearch.Text` when set), the separator an `<hr>` without a role; the empty part is written `data-aioros-empty hidden` with its text; every enabled option has the same class, the rest look with its text color and no selected background, its theme's `:selected` look being a rule under `[aria-selected=true]` whose color its texts inherit (WAI-ARIA's combobox with a listbox popup; cmdk's `aria-selected` and `data-selected`).

### ITEMS-112: Comboboxes list their items and post the choice

- **Given** a combobox (`Picker`, shaped as Aioros.Cosmo's `c:Combobox`: a trigger opening a popover that holds a `Finder` built in code over the combobox's items, its `SelectedItem` bound two way to a model's string among its bound strings)
- **When** the page is compiled
- **Then** the plan is reusable, the popover holds the filtered list's input and one option per string, the chosen one alone `aria-checked="true"` (ITEMS-122), and a post of the option's button with a typed search (`f0`) sets the model's selection to that string, as Avalonia's click on it does.

### ITEMS-122: Choosing lists are written for the browser

- **Given** a combobox (`Picker`, shaped as Aioros.Cosmo's `c:Combobox`) whose code names its `SelectedIndex` with `a:Web.Choice` and marks each option with `a:Web.Chooses` its index, whose template marks the trigger's text part `a:Web.ShowsChoice`, unbound with its second item chosen
- **When** the page is compiled
- **Then** the plan is reusable; the picker's element carries `data-aioros-choice` and `data-aioros-toggle` (its `SelectionMode` has `Toggle`) and holds a hidden field `f{n}` of value `1`, posted with every post, and one `<template data-aioros-value>` for each of `-1`, `0`, `1` and `2`, each holding the text part as written for that index (`data-aioros-shows`, the placeholder for `-1`, the item's text otherwise); each option carries `data-aioros-chooses` its index and `aria-checked`, `true` only for the chosen one, every option has the same classes, and its check's look under `:checked` is a rule under `[aria-checked=true]`; when its `SelectedItem` is bound two way, the field is written `disabled` with `data-change`, so it is posted only once the visitor chooses.

### ITEMS-123: Choosing lists bind the index a post carries

- **Given** the combobox of ITEMS-122
- **When** a post carries its field `2`
- **Then**
  - Its third item is chosen (and its bound model's string with it)
  - `-1` chooses none
  - `7` or `x` leave the choice as it was.

### ITEMS-126: Options are not tab stops

- **Given** the filtered list of ITEMS-110 followed by a button
- **When** the page is compiled
- **Then** every option is written `tabindex="-1"`; and when, in Chromium, the visitor focuses its input and presses Tab, then the focus goes to the button after the list, never to an option. cmdk's items are not tab stops: the input keeps the focus, `aria-activedescendant` names the selected option, which shows its theme's selected look (ITEMS-110), and the keys of ITEMS-111 move it.

## Filter script

### ITEMS-111: Typing filters ranks and selects as cmdk does

- **Given** the markup of ITEMS-110 in Chromium
- **When** the page loads
- **Then** the filter module (`/_aioros/filter/{hash}.js`, loaded only by pages holding `data-aioros-filter`) selects the first enabled option and names it in the input's `aria-activedescendant`, the listbox in `aria-controls`
- **When** the visitor types
- **Then**
  - Each option is scored by cmdk 1.1.1's `command-score` of its `data-value` or text (its `kbd` shortcuts read after its text, as cmdk reads shadcn's `CommandShortcut`) and hidden at 0, the options and the groups are ordered by their best score, the groups left empty and the separators are hidden, the empty part shows when nothing matches, and the first enabled option is selected; clearing the search gives the items back in their order
  - Down and Up (Ctrl+N or Ctrl+J, Ctrl+P or Ctrl+K) move the selection over the enabled options without wrapping, Alt with them to the next or previous group, Meta with them to the ends, Home and End to the ends; the pointer selects the option it moves over; and Enter clicks the selected option, which posts its button without submitting the form otherwise.

### ITEMS-125: Selecting an option scrolls only its list

- **Given** the markup of ITEMS-110 between two blocks 1200 px high, its listbox 72 px high and scrolling, in Chromium 600 px high (Aioros.Cosmo's command page, whose palette sits below the fold)
- **When** the page loads
- **Then** the filter module selects the first option without scrolling the page (`scrollY` 0)
- **When** the visitor focuses the input and moves the selection with Down past the listbox's bottom
- **Then** the listbox scrolls to keep the selected option inside it and the page does not move. cmdk's `scrollSelectedIntoView` keeps the item in view on a keyboard move only, never on the first render.

## Data table

### ITEMS-113: Headers are written as cells are

- **Given** a `TableView` shaped as Aioros.Cosmo's data table (its control themes), whose column headers are a `CheckBox` bound to the model's `AllPageRowsSelected`, a text, a `Button` with a command and a parameter around a `TextBlock`, a `TextBlock` aligned right in a column whose content stretches, and a header shown through the column's `HeaderTemplate`
- **When** the page is rendered
- **Then**
  - Each `<th>` holds its header as Avalonia renders it, written as a cell's content is: the check box a field, checked when the model's value is true; the button a submit button naming its command and parameter; the template's content; the text as text; no header reads a type's name (`Avalonia.Controls.CheckBox`).
  - A column whose content stretches takes its alignment from the content of its header and of its cells, so the right-aligned header and the right-aligned amounts are `class="r"`.
  - When the page is compiled with no row selected and rendered with every row selected, the plan is reusable and writes Avalonia's render.

### ITEMS-114: Bound column visibility is followed

- **Given** a `TableView` whose columns' `IsVisible` are bound to the model (`StatusColumn.IsVisible`, a column of Aioros.Cosmo's `DataTableSource`), with pixel widths and a star column
- **When** the page is compiled with every column shown and rendered for models hiding one column and two, and compiled with a column hidden and rendered with it shown
- **Then**
  - Each plan is reusable and writes Avalonia's render for each model: a hidden column has no `<col>`, `<th>` or `<td>`, and the star column's width is what the shown pixel columns leave.
  - A visibility bound through an index of the model's columns is ENGINE-244's.

### ITEMS-115: A row takes the look of its items flag

- **Given** Aioros.Cosmo's data table, whose rows take the `selected` class while their item's `IsSelected` is true (`DataTable.SelectsRows`, the library's code following the item), each row holding a check box bound two way to `IsSelected`
- **When** the page is compiled with no row selected and rendered for models with the second and fourth rows selected and with every row selected
- **Then**
  - The plan is reusable and writes Avalonia's render for each: a selected row's `<tr>` has the look Avalonia gives it (`#f5f5f5`) and the others the plain look, each with its hover look.
  - The plan learns both looks from the first row, its check box checked and then given back during the trace, so the model keeps its value.

*Precedent: TanStack Table's row, whose `data-state="selected"` and check box read the same row selection.*

### ITEMS-116: A tables two way fields and a delayed text post on change

- **Given** Aioros.Cosmo's data table and its filter, a `TextBox` bound two way to `Source.FilterText` with `a:Web.Delay="300"` (so the desktop filters on every keystroke, as shadcn/ui's does, and the web after a pause)
- **When** the page is rendered
- **Then** the table's box carries `data-change`, since check boxes of its header and rows are bound two way, and the filter's `<input>` carries `data-change data-delay="300"` (TEXT-41); a text box bound two way without a delay, and a table whose cells bind nothing two way, carry neither.

*Precedent: LiveView's `phx-debounce`; htmx's `hx-trigger="input changed delay:300ms"`; TanStack Table, whose filter input and row check boxes set the table's state as they change.*

### ITEMS-117: A tables changed field is bound after the others

- **Given** Aioros.Cosmo's data table with its second row selected (so the header's two-state check box shows the mixed state, `null`)
- **When** a post names the fourth row's check box in `_chg`, checked
- **Then** the second and fourth rows are selected and no other, no row but the fourth changed on the way, the header's check box is still mixed, and the answer writes both rows selected and `2 of 5 row(s) selected.`
- **When** a post names the header's check box in `_chg`, checked
- **Then** every row is selected
- **When** a post names the filter in `_chg` with `abe`
- **Then**
  - The answer shows Abe45's row alone, still selected, and the other rows keep their selection.
  - The field a post names in `_chg` is bound after the others, as the visitor changed it after them, and a two-state check box showing the mixed state that a post leaves unchecked keeps its value, as the visitor did not change it.

### ITEMS-119: Star columns share their room as percentages

- **Given** a `TableView` with a 100 px column and star columns of `128*`, `202*` and `162*` (Aioros.Cosmo's `table-demo`), a `TableView` whose star column's visibility is bound, and a `DataGrid` with a star column and an 80 px one
- **When** the pages are rendered
- **Then** each star column's `<col>` is its share of the stars as a percentage (`26.016%`, `41.057%`, `32.927%`; `100%` alone), and with a bound visibility its share of the shown stars (`calc(100% * 128 / var(--ts))`), never a `calc` that mixes a percentage and pixels, which Chromium's fixed table layout treats as `auto` and shares equally (164 px each); the fixed layout gives the pixel columns their width and scales the percentages into the rest, so a 592 px table has columns of 100, 128, 202 and 162 px, as Avalonia lays them out and shadcn/ui's reference shows (checked in Chromium).

*Precedent: CSS 2.1's fixed table layout and Chromium's TablesNG, which scale percentage columns down to the width the fixed columns leave.*

### ITEMS-120: Headers take their own padding

- **Given** Aioros.Cosmo's data table, whose `TableViewColumnHeader` theme is 40 px high with a padding of 8 px left and right and none above and below, its Email header a 36 px ghost button, and a Fluent `TableView`
- **When** the page is rendered
- **Then** the themed table's `<th>`s take the header's own padding (`padding:0px 8px 0px 8px`, the first and the last header's sides included) instead of the cells' 8 px all round, so the header row holding the button stays 40 px high as Avalonia lays it out (it was 52 px, the fixture 12 px taller); the Fluent table's headers keep the cells' padding.

## Bound carousel script

### ITEMS-124: A bound carousel posts each move once

- **Given** a carousel of four slides whose selection is bound (its hidden field in the selected slide, the carousel marked `data-change`), scrolling smoothly, and a server whose answer selects the posted slide
- **When** its next button is clicked once
- **Then** exactly one post names slide 1 and, once the strip has settled, slide 1 is selected
- **When** it is clicked twice quickly and the answer to the first of those posts is held until the strip has scrolled past slide 2
- **Then** the last post names slide 3, the model and the strip end on slide 3, and no move is posted twice: an answer that arrives while the strip is still scrolling selects the slide the scroll is going to, not the one it is passing.

## Tab template

### ITEMS-127: The panel takes its hosts margin

- **Given** a `TabControl` whose theme lays its strip and its content host in a `Grid`, the host spaced from the strip by its own margin (8 px above it, or 8 px to its left with `TabStripPlacement="Left"`, as Aioros.Cosmo's tabs)
- **When** Chromium lays the page out
- **Then** the selected panel's text stands where Avalonia places it on the desktop, the panel taking the host's margin; and a strip and host in a `StackPanel` keep its spacing (ITEMS-101).

*Precedent: Avalonia lays the host out with its margin in whatever panel the template uses; CSS margins on a flex item.*

### ITEMS-128: The tab list takes its panels spacing

- **Given** a `TabControl` whose items panel is a `StackPanel` with a `Spacing` of 4 (Aioros.Cosmo's line tabs), horizontal and vertical
- **When** Chromium lays the page out
- **Then** every tab's header stands where Avalonia places it on the desktop, the tab list taking the panel's spacing as its gap.

### ITEMS-129: A tab writes its presenters line height

- **Given** a `TabItem` with no height of its own whose template's content presenter sets `TextBlock.LineHeight` to 20 (Aioros.Cosmo's vertical tabs), and tab items with a `Width` of 90
- **When** Chromium lays the page out
- **Then** each tab is as tall as on the desktop (its padding, border and a 20 px line) and as wide as its width, and every header stands where Avalonia places it, the tab writing the presenter's line height and its own size.

### ITEMS-130: The selected pipe follows its part

- **Given** a `TabItem` theme whose template holds a `PART_SelectedPipe` border shown only while the tab is selected (opacity 0, 1 when selected), stretched along the tab's bottom 5 px below it (a 2 px line with a bottom margin of -5), and one stretched along its right side 4 px out (a 2 px line, right-aligned, right margin -4) with `TabStripPlacement="Left"`
- **When** Chromium lays the page out
- **Then**
  - The selected tab's line covers the same pixels as Avalonia's, placed from the part's alignment and margins, and no line is written for a tab control whose pipe stays hidden when selected (Aioros.Cosmo's default tabs)
  - Fluent's pipe keeps its place inside the tab's padding.

*Precedent: Fluent's `PART_SelectedPipe`; shadcn/ui's line tabs (`after:absolute after:bottom-[-5px]`, `after:-right-1`).*

### ITEMS-131: A tab takes its themes focus look

- **Given** a `TabItem` theme whose `:focus-visible` style gives its `PART_LayoutRoot` a `#a1a1a1` border and a `0 0 0 1 #a1a1a1, 0 0 0 3 #80a1a1a1` shadow, and a `:selected:focus-visible` style a transparent border (Aioros.Cosmo's tabs, the line tabs in the dark theme)
- **When** the page is rendered and Chromium gives the selected tab a visible keyboard focus
- **Then** the tab computes that border and those shadows, the selected one its own border, read from the theme as the tab's hover, pressed and selected looks are (ITEMS-101), and no tab shows them without the focus.

*Precedent: shadcn/ui's tab trigger (`focus-visible:border-ring focus-visible:ring-[3px] focus-visible:outline-1`); the buttons' focus looks Aioros reads from their themes.*

### ITEMS-132: A fluent panel takes its padding once

- **Given** a Fluent `TabControl` (its own theme, whose `PART_SelectedContentHost` takes the control's `Padding` as its margin through a template binding) with two tabs whose selected panel holds a text
- **When** the page is rendered
- **Then** in Chromium the tab headers and the panel's text stand where Avalonia places them (the panel's offset is the host's margin once, never the control's padding added to the margin that already carries it), while a template whose host has a margin of its own and no template binding (ITEMS-127 to ITEMS-131, Aioros.Cosmo's tabs) keeps that margin.

*Precedent: Fluent 12.1.3's `TabControl` template (`PART_SelectedContentHost` and the transition's `PART_SelectedContentHost2`, both `Margin="{TemplateBinding Padding}"`, read with ilspycmd), Avalonia applying a control's padding only where its template reads it, Avalonia's layout, which applies a template part's margin once.*

### ITEMS-133: A padding around the strip and the panel is written around both

- **Given** a `TabControl` theme whose root border reads the control's `Padding` (`Padding="{TemplateBinding Padding}"`) around a dock panel holding both the items presenter and the content host (as Avalonia's Simple theme draws it), with `Padding` 20,10 and two tabs whose selected panel holds a text
- **When** the page is rendered
- **Then**
  - In Chromium the tab headers and the panel's text stand where Avalonia places them: the padding is written once, around the strip and the panel together, not on the panel alone; a theme whose part reading the padding wraps only the content host gives it to the panel, and the hosts that take it as their margin (Fluent, Semi.Avalonia: ITEMS-132) keep it once.
  - Checked on 2026-10-21: no theme in use has this shape (Fluent's and Semi.Avalonia's hosts read it, Ursa defines no tab control, Aioros.Cosmo's template does not read it).

*Precedent: Avalonia applying a control's padding only where its template reads it; Avalonia's Simple theme `TabControl`.*

## Date picker theme

### ITEMS-144: A template bound selection posts as it changes

- **Given** a themed date picker whose template's popup holds a `ComboBox` whose `SelectedIndex` is a two-way `TemplateBinding` of the picker's property (as Aioros.Cosmo's presets bind `c:DatePresets.Index`)
- **When** it is rendered
- **Then** its `<select>` carries `data-change`, as a selection bound two way to the model does (ITEMS-18), so the framework's script posts it as it changes.
