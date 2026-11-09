# Controls: navigation

Panes, disclosure, menus and flyouts (`src/Adapters/Aioros.Avalonia/Html/Families/NavigationFamily.cs`).

60 criteria. Each has an identifier and exactly one test.

## Navigation controls

### NAVIGATION-01: Split view panes are drawers

- **Given** a `SplitView` with an `a:Web.Id`
- **When** it is rendered
- **Then** its pane is an `<aside popover>` with that id, followed by its content, and its style sheet turns the pane into a drawer below `a:Web.CollapseBelow`.

### NAVIGATION-02: Expanders are details

- **Given** an `Expander`
- **When** it is rendered
- **Then** it is a `<details>` whose `<summary>` holds the header, `open` when it is expanded, with the content after the summary; the content of a closed expander is written with the styles that apply to it, as when it is open.

### NAVIGATION-03: Menus are toolbars of popover menus

- **Given** a `Menu` of `MenuItem`s
- **When** it is rendered
- **Then** it is a `<menu role="menubar" data-aioros-overlay>` toolbar with one `<li role="none">` per item, and an item with sub-items is a `type="button"` `role="menuitem"` with `aria-haspopup="menu"` that opens an anchored `<div popover role="menu" class="menu">` holding them, placed as the item's template popup places its sub-menu.

### NAVIGATION-04: Menu items with commands submit them

- **Given** a `MenuItem` with a `Command` in a `MenuFlyout` (its `MenuFlyoutPresenter`)
- **When** it is rendered
- **Then** it is a `<button type="submit" name="_btn" role="menuitem" tabindex="-1" class="mi …">` whose value is its field id and command parameter, `disabled` when it is not enabled
- **When** that value is posted
- **Then** its command runs.

### NAVIGATION-05: Menu items show icons gestures checks and separators

- **Given** menu items with `a:Web.Href`, an icon, an input gesture, a toggle, an access key and separators
- **When** they are rendered
- **Then** the item is an `<a role="menuitem" class="mi …">` link with the icon as SVG, the header without its access-key underscore, the gesture in a `<kbd>`, a checked toggle `role="menuitemcheckbox" aria-checked="true"` with the check its theme draws as an SVG laid over it, and each `Separator` or `-` header is an `<hr>` with its theme's height, margin and fill.

### NAVIGATION-06: Submenus are nested popovers

- **Given** a `MenuItem` with sub-items inside a menu
- **When** it is rendered
- **Then** it is a `type="button"` `role="menuitem"` with `aria-haspopup="menu"` and its theme's chevron that opens a nested `role="menu"` popover placed beside it with CSS anchor positioning as its template's popup (Fluent's `RightEdgeAlignedTop`: `position-area:right span-bottom` with the popup's offset).

### NAVIGATION-07: Flyouts are popovers with the presenter look

- **Given** a `Button` with a `Flyout` (its `FlyoutPresenter`)
- **When** it is rendered
- **Then** the flyout is an anchored `<div popover role="dialog" class="fly …">` with the presenter's look as Avalonia shows it (the template's root border's background, border and radius, the room between it and the content: Fluent's `FlyoutContentThemePadding`) holding the flyout content.

### NAVIGATION-08: Tool tips are CSS bubbles

- **Given** a control with `ToolTip.Tip` and `ToolTip.ShowDelay`
- **When** it is rendered
- **Then** it is wrapped in a `display:contents` `<span class="tip" data-aioros-overlay>` with its `ToolTip.BetweenShowDelay`, the control is `aria-describedby` the `<span role="tooltip">` that follows it, and the tip is shown by CSS on hover after the show delay and on keyboard focus (`:focus-visible`) with the bubble of the `ToolTip` as its theme realizes it.

### NAVIGATION-09: Notification cards are status cards

- **Given** a `NotificationCard` with a `Notification` content and one with a text
- **When** it is rendered
- **Then** it is written through its control theme as Avalonia shows it at rest (Fluent's card: its shadowed border, its 4 px bar in the color of its `NotificationType`, its content), not mid-way through the entrance animation its theme runs.

### NAVIGATION-10: Command bars are toolbars

- **Given** a `CommandBar`
- **When** it is rendered
- **Then** it is a `<menu>` toolbar with its content first, each `CommandBarButton` as a submitting `<button>` with its icon and label, each `CommandBarSeparator` as an `<li role="separator">`, and its secondary commands in a "more" popover menu.

### NAVIGATION-11: Command bar toggle buttons are checkbox fields

- **Given** a `CommandBarToggleButton`
- **When** it is rendered
- **Then** it is a `<label>` holding a checkbox field (`checked` when it is checked) with its icon and label
- **When** a post omits it
- **Then** it becomes unchecked.

### NAVIGATION-12: Transitioning content enters with a CSS animation

- **Given** a `TransitioningContentControl`
- **When** it is rendered
- **Then** its content is in an element with a CSS enter animation taken from its `PageTransition` (a fade for `CrossFade`, a slide for `PageSlide`) and no animation without a transition.

### NAVIGATION-13: Validation errors follow their fields

- **Given** a bound field whose binding reports validation errors (a setter exception or `INotifyDataErrorInfo`)
- **When** the page is rendered after a post
- **Then** each error message follows the field in a `<div role="alert">` with the theme's error color; a page whose fields are bound to an `INotifyDataErrorInfo` model is planned: each field's errors are a plan slot that reads the model's current errors, so a post applied by the plan shows the same messages as Avalonia's post, and a field followed by an error message has the theme's error border; a button in such a page (a command, not a value) has no errors of its own and keeps the page planned.

### NAVIGATION-14: Refresh containers write only their content

- **Given** a `RefreshContainer`
- **When** it is rendered
- **Then** only its content is written: pull to refresh is the browser's own reload of the page.

### NAVIGATION-15: Content pages are sections with their bars

- **Given** a `ContentPage` with top and bottom command bars
- **When** it is rendered
- **Then** it is a `<section>` with the page background and the top bar, the content and the bottom bar in order.

### NAVIGATION-16: Navigation pages have a bar with title and back link

- **Given** a `NavigationPage`
- **When** it is rendered
- **Then** it is a `<header>` bar with the current page's header as its title (and a back link when the navigation page has `a:Web.Href`), followed by the current page.

### NAVIGATION-17: Tabbed pages are radio tabs

- **Given** a `TabbedPage`
- **When** it is rendered
- **Then** its tabs are radio buttons of one field whose `<label>`s show the page headers and the checked one shows its page by CSS alone
- **When** a post selects a tab
- **Then** it becomes the selected page; the page is planned, the checked tab follows the model on every render, and the plan applies a post that selects a tab without the engine.

### NAVIGATION-18: Drawer pages are popover drawers

- **Given** a `DrawerPage` open beside its content (`DrawerLayoutBehavior="Split"`, `IsOpen`)
- **When** it is rendered
- **Then** its drawer is an `<aside popover>` with the drawer header, the drawer and the footer, a toggle button opens it, and below `DrawerBreakpointLength` it becomes an overlay drawer.

### NAVIGATION-106: Drawer pages dock as Avalonia resolves them

- **Given** `DrawerPage`s as Avalonia resolves their display mode (its `ResolveSplitViewDisplayMode`)
- **When** they are rendered
- **Then** one open beside its content without a breakpoint (`Split`, as Semi.Avalonia's demo shell, or `CompactInline`) is docked at every width, while one whose layout overlays its content (the default `Overlay`, even with a breakpoint, or `CompactOverlay`), one in `Split` that is closed, and one whose behavior is `Flyout` are overlay drawers at every width, and a `Locked` one is docked at every width; the SemiDemo pages had their navigation pane hidden on the web, every page drawn 300 px left of Avalonia's.

### NAVIGATION-19: Carousel pages are scroll snap strips

- **Given** a `CarouselPage`
- **When** it is rendered
- **Then** its pages are a horizontal scroll-snapping strip, one page per view, starting at the selected page.

### NAVIGATION-20: Page navigation hosts write their page

- **Given** a `PageNavigationHost`
- **When** it is rendered
- **Then** its page is written in its place.

### NAVIGATION-21: Links to the current page are styled

- **Given** links (`a:Web.Href`, literal or bound) and a style for their `:current` pseudo-class
- **When** the page is compiled and rendered for a request path
- **Then** the link to the current page is marked `aria-current="page"`, the style applies to it through `[aria-current="page"]` (to its descendants too), the other links are not marked (a link with `a:Web.Exact` only on its own path), and a link without a `:current` style is marked by the same rule, since the desktop gives every `a:Web.Href` link the `:current` pseudo-class (DESKTOP-08) and the mark is derived from it (as React Router's `NavLink` and Next.js's active links state `aria-current="page"` whatever their style), and the plan is reusable, so a layout needs no view-model flag for its active link.

### NAVIGATION-22: Command bar icons keep their size

- **Given** `CommandBarButton`s whose icons are `a:Icon`s of the default size (16) and of `Size="24"`
- **When** they are rendered
- **Then** each icon's `<svg>` is its own size (`width="16" height="16"` and `24`), as Avalonia's template presents the icon at its size, so the label below it sits where Avalonia lays it out (moved down by half the difference, 4 px, as the face is centered).

### NAVIGATION-23: Content page themes inset the content

- **Given** a `ContentPage` whose control theme's template wraps its content presenter in an element with a margin (ControlCatalog's `ScrollPage`: a `ScrollViewer` with `Margin="20,20,5,0"`)
- **When** it is rendered
- **Then** its content area is inset by the margins between the page and the presenter (`padding:20px 5px 0px 20px`), so the content sits where Avalonia lays it out (20 px right and down); with Fluent's own theme it is not inset.

### NAVIGATION-24: Shared menu flyouts run their items command

- **Given** a `MenuFlyout` defined once as a resource and set as the flyout of a button in every item of a list, its item's command bound to the page's model by `#name` with the list item as its parameter (a parameter whose text is the same for every item)
- **When** the second item's menu item is posted
- **Then**
  - The command runs once with the second item (posts of menu items are applied by the Avalonia engine; the plan's forms do not describe menu items yet); the shared menu takes the data context of the button it is written for, as Avalonia gives it to the button that opens it.
  - Found porting AdvancedToDoList (its to-do list's item menu).

### NAVIGATION-25: Flyout fields bind when posted

- **Given** a `Button` whose `Flyout` holds a `ComboBox` bound two ways to the model and a button with a command
- **When** the form is posted with another option and the flyout's button
- **Then**
  - The model takes the posted option and the command runs once: the flyout's fields keep the data context they were written with, although the flyout is closed again after it is written.
  - Found porting AdvancedToDoList (its sort flyout).

### NAVIGATION-26: Templated controls in flyouts are written with their template

- **Given** a `Flyout` holding a templated control (a `HeaderedContentControl` with its own control template, as a form's labeled field is)
- **When** the page is compiled
- **Then** the plan is reusable and the flyout's popover holds the control as its template builds it (its header and its content), the same elements it is written as outside a flyout, instead of an empty element.

### NAVIGATION-46: Validating models plan the errors of every validated binding

- **Given** a page on a validating model (CommunityToolkit's `ObservableValidator`, `INotifyDataErrorInfo`) with a read-only `TextBox` bound one way to a validated member, a `TextBox` bound two-way, a `CalendarDatePicker` and a templated control whose template part (a `TextBox` bound with `TemplateBinding`, as the parts of Ursa's pickers are) inherits the model as its data context
- **When** the page is compiled with a valid model
- **Then** the plan is reusable, and rendered for a model whose members all have errors it writes the error messages Avalonia renders, in its order (the field's error look is the plan's error border, NAVIGATION-13): each error after the control whose binding reports it (the one-way binding included, since Avalonia validates every binding of a property that enables data validation), and nothing for the template part, which binds no member of the model; rendered for a valid model, the plan writes Avalonia's render byte for byte.

### NAVIGATION-47: Bound tool tips follow their text

- **Given** a `Border` whose `ToolTip.Tip` is bound to a model's text (as AdvancedToDoList's category badge shows its description)
- **When** the page is compiled with a model whose text is null and with one whose text is set
- **Then** both plans are reusable and each writes, for either model, what Avalonia renders: the tip's wrapper and bubble with the text when it is set (NAVIGATION-08), and the bare control when it is null, since Avalonia shows no tip without content.

### NAVIGATION-48: Errors set in the XAML are written as rendered

- **Given** controls whose errors are set in the XAML and bound to nothing (`DataValidationErrors.Error` holding an exception on a `TextBox` and a `NumericUpDown`, as Semi's demo shows its error looks)
- **When** the page is compiled
- **Then** the plan is reusable and writes the errors as Avalonia renders them for any model, since they are constants of the page; a control whose errors come from a validated binding still has them planned or fails the plan (NAVIGATION-13, NAVIGATION-46).

### NAVIGATION-70: Popovers in list rows are numbered per row

- **Given** a button with a menu flyout in the item template of a bound list (as AdvancedToDoList's task rows) between buttons with flyouts outside the list
- **When** the page is compiled and rendered for lists of other lengths
- **Then** the plan is reusable and each render, also after the plans file, numbers the popovers as Avalonia's render does, in document order: each row's button and its menu share an id of their own (`popovertarget`, `anchor-name`, the popover's `id` and `position-anchor`), no two rows share one, and the buttons after the list follow.

### NAVIGATION-71: Converted tool tips follow their text

- **Given** a `Border` whose `ToolTip.Tip` is bound through a converter that turns the model's text into another text or null (as Semi's color palette names each swatch through Avalonia's `ColorToDisplayNameConverter`)
- **When** the page is compiled with a model whose text is null and with one whose text is set
- **Then** both plans are reusable and each writes, for either model, what Avalonia renders: the tip with the converted text when the converter gives one, and the bare control when it gives null.

### NAVIGATION-81: Template parts bound to their control show no model errors

- **Given** a page on a validating model (`INotifyDataErrorInfo`) with a templated control whose template's `ComboBox` binds its `SelectedItem` two-way to a property of its templated control (`{Binding RelativeSource={RelativeSource TemplatedParent}, Path=Tag, Mode=TwoWay}`, as the offset combo box of Ursa's `DateTimeOffsetPicker` binds `SelectedOffset`)
- **When** the page is planned
- **Then** the plan is reusable; rendered for a model with errors it writes the error messages Avalonia renders, those of the page's own validated fields and none for that part, since its binding validates the control and not the model; rendered for a valid model it writes Avalonia's render byte for byte.

### NAVIGATION-91: Themed expanders are written through their template

- **Given** `Expander`s whose control theme (not Fluent's) draws a 1 px bottom border and holds an `ExpanderHeader` `ToggleButton` whose own theme is a `PART_Root` border with a 16 px vertical padding around the header's presenter and a trailing chevron in a `Viewbox` turned 180° under `:checked`, underlined under `:pointerover`, and a content presenter with a 16 px bottom padding, as Aioros.Cosmo's accordion items, one expanded, both inside a panel with `a:Web.Exclusive="True"`, and a Fluent `Expander` beside them
- **When** the page is rendered
- **Then** each themed expander is a `<details>` (`open` when expanded) with its border, whose `<summary>` has the header toggle's box, padding and states (the underline on `:hover`) and holds the toggle's template parts in order (the header's text, then the chevron's `<svg>`, with no chevron of Aioros's own), the chevron turned 180° while the details are open (`details[open]>summary …{transform:…}`), and whose content follows in a box with the content presenter's padding; the two share one `name`, so the browser keeps one of them open (HTML's exclusive accordion); the Fluent expander is written as before (NAVIGATION-02), with no `name`.

### NAVIGATION-92: Data items of expander lists take their templates

- **Given** a library `ItemsControl` bound to the model's list whose containers are `Expander`s of the theme of NAVIGATION-91, each showing its item as its header through the list's `ItemTemplate` and as its content through the list's `ContentTemplate` (as Aioros.Cosmo's `c:Accordion` of data items), inside a panel with `a:Web.Exclusive="True"`
- **When** the page is compiled and rendered for other lists
- **Then** the plan is reusable and writes Avalonia's render for each list (also after the plans file): each item a `<details name>` whose summary shows the header template's text for that item and whose content shows the content template's, not the item's `ToString`.

### NAVIGATION-95: A collapsible is written through its panel template

- **Given** an `Expander` whose theme (Aioros.Cosmo's collapsible) stacks its `ExpanderHeader` toggle and its content presenter with an 8 px spacing, the toggle's own template a `Panel` (no named border) that overlays the header's presenter with a 32 px `PART_Toggle` border at the top right holding a chevrons icon, the border filled `#f5f5f5` under the toggle's `:pointerover`
- **When** the page is rendered open and closed
- **Then** it is written through its template: the `<summary>` holds the panel, the header's content and the toggle box with its icon at the top right (`justify-self:end`), and no chevron of Aioros's own; the toggle box takes its fill while the summary is hovered (`details>summary:not(:disabled):hover .x{background:#f5f5f5!important}`), as shadcn/ui's ghost trigger does; and the content follows the summary 8 px below it (`margin-top:8px`), the template's spacing.

### NAVIGATION-98: A post from a flyout keeps it open

- **Given** a `Button` whose `Flyout` holds a `ComboBox`, a `Step` button without a command and an `Apply` button with one, beside a `Button` outside it
- **When** a post presses `Step`
- **Then** the answer writes the flyout's popover open (`data-aioros-open`), as the visitor could press it only while the browser showed it
- **When** a post comes from the combo box's change (`_chg` naming its field)
- **Then** the answer writes it open too
- **When** a post presses the button outside, or `Apply`, whose command closes the flyout as a menu item closes Avalonia's `MenuFlyout` and a link closes Radix's navigation menu
- **Then** the popover is written closed.

*Precedent: Radix's `Popover` (it stays open while its content is used) and Aioros's own date picker, whose popup a post from its month buttons keeps open (RANGES-24).*

### NAVIGATION-107: Links write only safe addresses

- **Given** links whose address is bound to data (a border's or a run's `a:Web.Href`) or literal (a `HyperlinkButton`'s `NavigateUri`)
- **When** they are rendered, by the engine or from a plan read back from the plans file
- **Then** a relative address and an `http`, `https`, `mailto` or `tel` one are written as they are, and any other scheme (`javascript:` in any case, with spaces, tabs or line breaks inside it, `data:`, `vbscript:`) is written as `#`, so a value a user controls cannot run script in the app's origin when another visitor follows it (the security audit's F1; precedent: Angular's URL sanitizer, React's blocking of `javascript:` URLs, OWASP's XSS prevention).

## Overlay theme

### NAVIGATION-82: Flyouts take their presenters theme

- **Given** a `Button` whose `Flyout` has a class in `FlyoutPresenterClasses` and a `FlyoutPresenterTheme` whose template's root border has a `BoxShadow`, with a width of 288, a padding, a radius, a border, a background and a font size (and a `^.wide` style setting the width to 320)
- **When** it is rendered
- **Then** the popover's look is the presenter's as Avalonia shows it for that flyout: `width:320px`, the root border's background, border, radius and shadow, its padding, the font size; a flyout with Fluent's presenter has `box-shadow:none`; neither has a transition or an animation of Aioros's own.

### NAVIGATION-83: Flyouts are placed as Avalonia places them

- **Given** flyouts placed `Bottom`, `Top`, `Left`, `Right`, `BottomEdgeAlignedLeft`, `BottomEdgeAlignedRight`, `TopEdgeAlignedLeft`, `TopEdgeAlignedRight`, `LeftEdgeAlignedTop`, `RightEdgeAlignedBottom` and `Center` with a `VerticalOffset` of 4 and a `HorizontalOffset` of 2
- **When** they are rendered
- **Then** each popover's `position-area` is the placement's (`bottom`, `top`, `left`, `right`, `bottom span-right`, `bottom span-left`, `top span-right`, `top span-left`, `left span-bottom`, `right span-top`, `center`), the offset toward its anchor is the margin on the anchor's side as Avalonia moves the popup by its offsets (`margin-top:4px` below, `margin-bottom:-4px` above, `margin-right:-2px` left, `margin-left:2px` right), the offset across is a `translate`, and it flips on its axis (`position-try-fallbacks:flip-block` or `flip-inline`).

### NAVIGATION-84: Attached flyouts open on hover or click

- **Given** a control with a `FlyoutBase.AttachedFlyout` shown `TransientWithDismissOnPointerMoveAway` and attached properties `OpenDelay` 500 and `CloseDelay` 200 of a library's (as a hover card declares them), a control with an attached flyout in the standard mode, and one shown `Transient` with no delay properties (WinUI's show modes of a flyout shown proactively are hover cards)
- **When** they are rendered
- **Then** the first is wrapped in `<span class="hv" data-aioros-overlay data-aioros-hover="m1" data-open="500" data-close="200">` holding the control and a `<div popover="hint" id="m1" role="dialog" class="fly …">` anchored to it with the flyout's content and its presenter's look (its `FlyoutPresenterClasses`), the second in `data-aioros-click`, an auto popover a click opens, and the third has Radix's 700 and 300 ms.

### NAVIGATION-85: Tool tips take their themes bubble and arrow

- **Given** a control with `ToolTip.Tip`, `ToolTip.Placement="Top"`, `ToolTip.VerticalOffset="0"`, `ToolTip.BetweenShowDelay="300"` and a `ToolTip` theme whose template draws the bubble's border with a padding, a radius and a 16 px line height, and a rotated square under it in a row of 10 px (an arrow)
- **When** it is rendered
- **Then** the tip is `position-area:top` with a `flip-block` fallback, the wrapper carries `data-between="300"`, the tool tip element is the bubble with the template's background, radius, padding and line height, held 10 px above the control (`margin-bottom:10px`, the row the template leaves under the bubble), and what the template draws there is an inline SVG placed from the bubble's bottom and middle as Avalonia places it (so it holds for a tip of any text), the look read once per theme and variant.

### NAVIGATION-86: Menu items take their themes looks

- **Given** a `MenuFlyout` whose `MenuFlyoutPresenter` theme has a shadowed border and a 4 px items margin, and items with a `MenuItem` theme (padding 8,6, a 4 px radius, a `:selected` fill, a `.destructive` foreground, a `.label` item that is not focusable with a medium weight, a `.inset` padding, a 16 px icon column, a gesture text of 12 px with a letter spacing, a radio item's dot drawn as an `Ellipse`)
- **When** it is rendered
- **Then** the menu has the presenter's look, each item's class carries its theme's radius, the room Avalonia leaves before its header and after its last part, its height, and a `:focus` rule with the `:selected` fill; the destructive item is in its foreground; the label is a `role="none"` row in the medium weight; the inset item's room before its header is 32 px; the gesture is 12 px with its letter spacing; the checked radio item is `role="menuitemradio" aria-checked="true"` with the dot drawn as an SVG `<ellipse>`-shaped path where Avalonia draws it.

### NAVIGATION-87: Toggle menu items post their click

- **Given** a `MenuFlyout`, and a `Menu` bar whose item opens a menu from its template's popup, each with a check box item without a command bound two-way to a model's flag, and the flyout with radio items of one group
- **When** a check box item's button is posted and then a radio item's
- **Then** the flag flips as Avalonia's item flips it on a click (in the flyout as in the bar's menu) and the posted radio item is checked and its group's other item unchecked, the answer writing them `aria-checked` as they are.

### NAVIGATION-88: Context menus open at the pointer

- **Given** a `Border` with a `ContextMenu` and another with a `ContextFlyout` of menu items, and a `TextBox` (whose Fluent theme gives a context flyout of cut, copy and paste)
- **When** they are rendered
- **Then** each border is wrapped in `<span class="ctx" data-aioros-overlay data-aioros-context="m1">` followed in it by a `<div popover role="menu" class="menu … cm">` with its items and the look of the surface Avalonia shows it in, placed at `--aioros-x` and `--aioros-y`; the text box has no context menu of Aioros's (the browser's own menu stays).

### NAVIGATION-93: Menu bars take their themes looks

- **Given** a `Menu` whose control theme (Aioros.Cosmo's menubar) is a 36 px bar drawn by a border with a box shadow around its items presenter centered vertically in a horizontal `StackPanel` with a `Spacing` of 4, and whose item container theme (padding 8,4, a 28 px item, an accent look under `:selected` and `:open`) opens its sub-items in its template's `Popup` placed `BottomEdgeAlignedLeft` with a `HorizontalOffset` of -4 and a `VerticalOffset` of 8, around a border with a `MinWidth` of 192, a box shadow and a `ScrollViewer` holding the items presenter with a `Margin` of 4
- **When** it is rendered
- **Then** the bar carries its template border's `box-shadow`, `align-items:center` and `gap:4px`; each top-level item is written with its theme's 8 px padding and 28 px height and its `:open` look under `:has(+:popover-open)` (Radix's `data-state=open`); its menu popover is placed `bottom span-right` with `margin-top:8px` and `translate:-4px 0px`, at `max-content` width with `min-width:192px`, the border's `box-shadow`, and a 4 px padding from the items presenter's margin inside the scroll viewer.

### NAVIGATION-94: Buttons marked for hover open their flyouts on hover

- **Given** a `Button` with a `Flyout` and `a:Web.HoverOpenDelay="200"` and `a:Web.HoverCloseDelay="150"` (Aioros.Cosmo's navigation menu trigger, which opens its content on hover in its own code), a button with only `a:Web.HoverOpenDelay="300"`, and a button with a flyout and no hint
- **When** the page is rendered
- **Then** the first `<button popovertarget>` carries `data-aioros-overlay data-hover-open="200" data-hover-close="150"`, the second `data-hover-open="300" data-hover-close="150"` (Radix's NavigationMenu closing 150 ms after the pointer leaves), and the third none of them.

### NAVIGATION-99: Switching popovers slide between each other

- **Given** two buttons of a row with flyouts opened on hover (`a:Web.HoverOpenDelay`) and `a:Web.SwitchOffset="208"` (Aioros.Cosmo's navigation menu triggers), whose presenters' root enters from opacity 0 and 8 px up and leaves at opacity 0 and 95% while `:closing`, and a third button with no switch offset
- **When** the page is written
- **Then** the first two carry `data-switch` and the third does not, and each switching popover's entrance class also has `@starting-style` rules for `[data-motion=from-end]` (its entrance moved `translateX(208px)` before its own transform) and `[data-motion=from-start]` (`translateX(-208px)`), and closed looks for `[data-motion=to-start]` (its closing look moved `translateX(-208px)`) and `[data-motion=to-end]` (`translateX(208px)`).

*Precedent: Radix NavigationMenu's `data-motion` with shadcn/ui's `slide-in-from-right-52`, `slide-out-to-left-52` and their mirrors, tw-animate-css composing the translation before the scale.*

### NAVIGATION-100: A tip themes text wrap is written on its bubble

- **Given** a `ToolTip` theme setting `a:Web.TextWrap="balance"` (Aioros.Cosmo's tooltip, shadcn/ui's `text-balance`)
- **When** a control showing a text tip in it is rendered
- **Then** the tip's bubble class carries `text-wrap:balance`, and a tip of a theme without it carries no `text-wrap`.

### NAVIGATION-101: A tips controls are styled as inside their tool tip

- **Given** a `Button` whose `ToolTip.Tip` is a `StackPanel` holding a text and a templated control, and a style `ToolTip Border.key` giving that control's template root a background (Aioros.Cosmo's key in a tooltip, shadcn/ui's `[[data-slot=tooltip-content]_&]` key)
- **When** the page is rendered
- **Then** the tip's bubble holds the text and the control written through its template, with the style's background, and the bubble takes the `ToolTip` theme's look (its `#171717` background) as a text tip does, as Avalonia shows the content inside its `ToolTip`.

### NAVIGATION-102: A wrapped tip is written at the width Avalonia lays it out at

- **Given** a `ToolTip` theme with a `MaxWidth` of 320 and a tip whose control content is a wrapping text longer than that
- **When** the page is rendered
- **Then** the bubble is written at the width Avalonia lays the tip out at (its widest line with the padding, within 1 px), not at its `MaxWidth`, as Avalonia's tip shrinks to its content (Semi.Avalonia's rich tooltip, 283 px under a 320 px `MaxWidth`); a tip that fits on one line carries no width of its own.

## Dialog post

### NAVIGATION-89: Notices stand where their panel stands them

- **Given** notices a post showed through a `WindowNotificationManager` at the top right (Fluent's reversible stack of cards) and through one whose theme stands its cards in a `Grid` at the bottom right with a margin of 24 (a deck), with a notification's default expiration and one whose expiration is zero
- **When** the answer is written
- **Then** the first notice is a column at `top:0px;right:0px`, the deck's is at `bottom:24px;right:24px` with each card where the grid arranged it (`position:absolute`, held at the bottom), each card (`role="status"`) fades after its expiration (`--expire:5000ms`) and the card that never expires has no fade (`nk`).

## State class

### NAVIGATION-90: A tip shown by one variant describes only that variant

- **Given** an item template whose `Button` shows a `ToolTip.Tip` only while a bound class is set (a style on `Button.chip.on`)
- **When** the page is planned with one item on and one off
- **Then** only the item showing the tip carries `aria-describedby`, and the plan writes what Avalonia renders for both variants (no AIOROS010).

## Expander theme

### NAVIGATION-96: An expanders content keeps its presenters box

- **Given** an `Expander` whose theme (Aioros.Cosmo's trace collapsible) stacks a toggle aligned to its start (`HorizontalAlignment="Left"`) over a content presenter with `Margin="7,8,0,4"`, `Padding="16,0,4,0"` and a 1 px left border of `#e5e5e5`, open, and an expander of the same theme whose toggle stretches and whose presenter in a stack panel spaced 8 px has neither margin nor border
- **When** the page is rendered
- **Then** the first content's box takes the presenter's margin (`margin:8px 0px 4px 7px`), its padding and its border (`border-width:0px 0px 0px 1px`, `#e5e5e5`, solid), and its summary is only as wide as its toggle (`width:fit-content`), as Radix's `w-fit` trigger; the second's summary keeps the expander's width and its content follows 8 px below it, as NAVIGATION-95's.

*Precedent: the radix base's `TraceCollapsibleContent` (`ml-[calc(--spacing(2)-1px)] border-l pl-4 mt-2 mb-1`) and `TraceCollapsibleTrigger` (`w-fit`); Aioros.Cosmo's need 71.*

### NAVIGATION-103: An expanders trigger keeps its own color

- **Given** two expanders of one theme whose trigger shares the expander's muted color, the first with a class turning its trigger red
- **When** the page is rendered
- **Then** the first's `<summary>` carries the trigger's red and the second's no color of its own (the expander's, inherited).

*Precedent: Avalonia's template parts styled from the templated parent's states, the radix base's `TraceCollapsibleTrigger` (`has-[[data-status=error]]:text-destructive`).*

### NAVIGATION-104: A chevron turned through a transition turns with its details

- **Given** an expander whose theme (not Fluent's) holds an `ExpanderHeader` toggle whose template's chevron `Viewbox` turns 90° under `:checked` through a `TransformOperationsTransition` on its `RenderTransform` (Aioros.Cosmo's sidebar group label), one expanded and one collapsed
- **When** the page is rendered
- **Then** each chevron carries a class turning it `none` while its details are closed and 90° while they are open, as the transition's settled values, not the value it is moving through when the page is traced.

*Precedent: Avalonia's transitions (`GetBaseValue` gives the value a transition moves to), NAVIGATION-91.*

### NAVIGATION-105: A toggle with its own height gives it to its summary

- **Given** an expander whose theme (not Fluent's) holds an `ExpanderHeader` toggle 32 px high by its own `Height`, its template a `Panel` holding a border padded 8 px across whose dock panel centers a 14 px label vertically (Aioros.Cosmo's sidebar group label)
- **When** Chromium lays the page out
- **Then** its `<summary>` is 32 px high and the label's line is centered in it, 6 px from its top, as Avalonia arranges the toggle; the content follows below the 32 px.

*Precedent: Avalonia's `Layoutable.Height` (the toggle's own height bounds its template), NAVIGATION-95.*

## Bound expander

### NAVIGATION-97: Two way expanders are posted

- **Given** a Fluent `Expander` and a themed one (Aioros.Cosmo's collapsible) whose `IsExpanded` is bound two way to the model's `Open`, and an expander bound one way
- **When** the page is planned and rendered for an open and a closed model
- **Then** the plan is reusable and writes what Avalonia renders; each two-way `<details>` is marked `data-change` and holds, right after its summary, a hidden check box (`<input type="checkbox" hidden name="f{id}">`), `checked` while the model's flag is on
- **When** a post carries it
- **Then** the model's flag is true, and when it leaves it out, false, through the plan and through the engine; the one-way expander writes neither.

*Precedent: Radix's controlled `Collapsible` (`open`, `onOpenChange`); ENGINE-259's flag posted through its check box.*

## Data table

### NAVIGATION-108: Columns toggled from a menu are answered from the plan

- **Given** a menu of `MenuItem`s with `ToggleType="CheckBox"` and `IsChecked` bound two way and no command (Aioros.Cosmo's data table's column menu)
- **When** the visitor presses one
- **Then** the press is answered from the plan, as a binding is, while Avalonia runs in the process too: the bound value is flipped as the item's click flips it on the desktop (the Status column hidden), with no command for the generator to have listed

## Container query

### NAVIGATION-109: A container query hides a tabs button

- **Given** a `TabControl` in a container whose `ContainerQuery` (`max-width:520`) hides its `TabItem.wide` with `IsVisible`
- **When** the page is written and laid out at 400 and 700 px
- **Then** at 700 every tab's button shows and at 400 the `.wide` tab's button does not, as Avalonia lays it out, so the same texts show at each width.
