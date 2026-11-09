# Focus

Focus declared in the markup a control writer or a library's slot renderer writes, run by the framework's script (`src/Targets/Aioros.Targets.Ssr/Documents/Scripts.cs`): an element that follows the finger with the focus (`data-aioros-follow`) and roving focus (`data-aioros-roving`, with `data-aioros-roving-escape` and `data-aioros-roving-ends`). The app never writes these attributes or any script; a library links `:focus` to its states in CSS (a chart's tooltip shown for its focused category).

11 criteria. Each has an identifier and exactly one test.

## Focus script

### FOCUS-01: The focus follows the finger

- **Given** an element marked `data-aioros-follow` holding descendants with `tabindex`
- **When** a finger touches one of them and moves over a second and a third
- **Then** each descendant under the finger takes the focus in turn (the nearest ancestor with `tabindex` of what `document.elementFromPoint` finds, inside the marked element), and when the finger lifts the last one keeps the focus; a touch on an element that is not inside a marked element moves no focus, and a finger over a part of the marked element with no focusable descendant leaves the focus where it was.

### FOCUS-02: Touch listeners let the page scroll unless captured

- **Given** an element marked `data-aioros-follow` in a page taller than the window
- **When** a finger drags up over it
- **Then** the page scrolls (the script's touch listeners are passive and prevent nothing)
- **When** an element marked `data-aioros-follow="capture"`, when the page is rendered
- **Then** the base styles give it `touch-action:none`, so the browser does not scroll while the finger moves over it and the focus still follows.

### FOCUS-03: Horizontal roving focus moves with the arrows

- **Given** an element marked `data-aioros-roving="horizontal"` holding four descendants with `tabindex`
- **When** the second has the focus and ArrowRight, ArrowLeft, End, ArrowRight, Home and ArrowLeft are pressed
- **Then**
  - The focus moves to the third, the second, the fourth, stays on the fourth, moves to the first and stays on the first (no wrapping, as Recharts' keyboard layer and the WAI-ARIA toolbar pattern stop at the ends), each handled key's default action prevented
  - ArrowUp and ArrowDown move nothing; whenever one of the descendants takes the focus, by a key or a click, it alone has `tabindex="0"` and the others `tabindex="-1"`; a key pressed in a text field inside the element that has no `tabindex` is left to the field.

### FOCUS-04: Vertical and both roving focus follow their arrows

- **Given** `data-aioros-roving="vertical"`
- **When** ArrowDown, ArrowUp, ArrowRight and ArrowLeft are pressed on its first descendant
- **Then** ArrowDown moves the focus to the second and ArrowUp back to the first, and the left and right arrows move nothing
- **When** `data-aioros-roving="both"`
- **Then** all four arrows move it.

### FOCUS-05: Escape blurs only when the roving element asks

- **Given** a roving element with `data-aioros-roving-escape`
- **When** Escape is pressed on its focused descendant
- **Then** that descendant loses the focus
- **When** a roving element without the attribute, when Escape is pressed
- **Then** the focus stays.

### FOCUS-06: Roving ends off leaves home and end alone

- **Given** a roving element marked `data-aioros-roving-ends="off"` holding three descendants with `tabindex`
- **When** the second has the focus and Home, End, ArrowRight and ArrowLeft are pressed
- **Then** Home and End leave the focus on the second and their default action is not prevented (as Recharts' `AccessibilityManager` ignores them), while ArrowRight moves it to the third and ArrowLeft back to the second, each prevented.

### FOCUS-11: Roving that keeps tabs moves over focusable items

- **Given** an element marked `data-aioros-roving="horizontal"` with `data-aioros-roving-tabs="keep"` and `data-aioros-roving-ends="off"` holding a button, a link, a popover's trigger button with its popover (holding a link) inside the element, and a disabled button, none with `tabindex`
- **When** the first button has the focus and ArrowRight is pressed three times and ArrowLeft once
- **Then** the focus moves to the link, the trigger, stays on the trigger (past it are only the disabled button and the popover's link, which the arrows skip) and back to the link, and no item's `tabindex` changes.

## Keyboard script

### FOCUS-07: Tabs switch with the click and the arrows

- **Given** a page whose markup has `data-aioros-tabs` boxes (ITEMS-05), one horizontal and one whose tab list is `aria-orientation="vertical"`, each with a disabled tab and its selected tab's hidden input inside a `data-change` group
- **When** the page loads
- **Then** the keyboard module (loaded only by pages that declare tabs or shortcuts) gives each tab and panel an id, each tab `aria-controls` naming its panel and each panel `aria-labelledby` naming its tab
- **When** a tab is clicked, or the focus moves with the arrows (left and right, or up and down when vertical, wrapping at the ends and skipping disabled tabs), Home or End
- **Then** that tab is selected at once (automatic activation): it alone has `aria-selected="true"` and `tabindex="0"`, its panel alone is shown, the hidden input names its index and a `change` event posts it, with no request to switch.

*Precedent: the WAI-ARIA Authoring Practices' tabs pattern with automatic activation, Radix Tabs' default (`activationMode="automatic"`, looping focus).*

### FOCUS-08: Shortcuts click their buttons

- **Given** buttons marked with key shortcuts (`data-aioros-key`, BUTTONS-96): one for Ctrl+K that opens a popover, one for Ctrl+Shift+S, and one for `/` without a modifier
- **When** the keys are pressed
- **Then**
  - The matching button is clicked (its popover opens, its post is sent) and the key's default is prevented
  - Meta+K works as Ctrl+K (cmdk's reading on macOS); a gesture without a modifier is ignored while the focus is in a text field and a gesture with one is not; keys that match no shortcut are left alone.

*Precedent: Avalonia's `HotKeyManager`, which runs a button's command from anywhere in its window; cmdk's Ctrl or Meta reading of a shortcut.*

### FOCUS-09: Scoped shortcuts take their scopes keys first

- **Given** key buttons (BUTTONS-97): a document-level Ctrl+K, a Ctrl+K marked `data-aioros-scope` right before a box holding a text field and an inner box, a disabled scoped Ctrl+J before the inner box with a document-level Ctrl+J, and a scoped `Delete` before the box
- **When** Ctrl+K is pressed with the focus in the box's field
- **Then** the scoped button is clicked and the document-level one is not
- **When** it is pressed with the focus outside the box (or on nothing)
- **Then** the document-level one is clicked
- **When** Ctrl+J is pressed inside the inner box
- **Then** the disabled scoped button is skipped and the document-level Ctrl+J is clicked
- **When** `Delete` is pressed while typing in the box's field
- **Then** nothing is clicked and the field keeps its default, and pressed on the box's button it is clicked.

*Precedent: Avalonia's `KeyboardDevice`, which walks from the focused element up through its ancestors' `KeyBindings` before the top level's, and a binding that cannot execute leaves the key unhandled for the next.*

## Keyboard navigation

### FOCUS-10: Keyboard xy navigation is written as roving focus

- **Given** a horizontal `StackPanel` with `XYFocus.NavigationModes="Keyboard"` holding buttons and a nested horizontal `StackPanel` (which inherits the mode), a vertical `StackPanel` with `XYFocus.NavigationModes="Enabled"`, a `WrapPanel` with `Keyboard` and `KeyboardNavigation.TabNavigation="Once"`, and a panel with neither
- **When** the page is rendered
- **Then** the first is `data-aioros-roving="horizontal"` and its nested panel carries no roving mark, the second `vertical`, the third `both`, each with `data-aioros-roving-ends="off"` (Avalonia's arrows move the focus; Home and End are not part of XY navigation), the first two with `data-aioros-roving-tabs="keep"` (every item stays in the tab sequence, as `TabNavigation` other than `Once` keeps it) and the third without (one tab stop), and the last carries no roving mark.

*Precedent: Avalonia's `XYFocus.NavigationModes`, which the desktop runs with the arrow keys, and `KeyboardNavigation.TabNavigation`; Radix's navigation menu (the arrows between its items, each still a tab stop).*
