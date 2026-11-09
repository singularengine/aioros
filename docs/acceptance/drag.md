# Drag and drop

Sortable lists and moves between lists, declared in XAML with the `a:Drag` attached properties (`src/Adapters/Aioros.Avalonia/Controls/Drag.cs`): `Drag.Item` gives an element its item key, `Drag.Target` makes a list a drop target with its own key, and `Drag.Command` is the view model's command, which receives the move as `(string Item, string Target, int Index)`, the index the item has in the target list after the move. On the desktop the behavior runs Avalonia's own drag and drop; on the web the framework's script moves the element at once, locks the lists until the server answers, lets the server's answer win and rolls the move back on an error, with a keyboard path and, without JavaScript, move buttons inside the page's form.

12 criteria. Each has an identifier and exactly one test.

## Drag

### DRAG-01: Drag declarations mark targets and items

- **Given** an `ItemsControl` bound to a list with `Drag.Target` and `Drag.Command` bound and items whose template sets `Drag.Item`
- **When** the page is rendered
- **Then** the list's element carries `data-drop` with the target's field token, every item's element carries `data-drag` with its key, `tabindex="0"` and `aria-roledescription="draggable"`, and holds a `<noscript>` with "Move up" and "Move down" buttons posting `_btn` as the target's token, `up` or `down`, and the item key; the plan is reusable and renders another model's lists and keys the same way Avalonia does.

### DRAG-02: Posted moves run the targets command

- **Given** that list
- **When** a post names the target's token, an index and an item key (`_btn=token|1|key`)
- **Then** the target's command runs once with `(key, target key, 1)` in Avalonia's engine and in the plan engine alike, and a post of `up` or `down` runs it with the item's current index minus or plus one.

### DRAG-03: Moves outside the list run nothing

- **Given** that list
- **When** a post names an index past the end of the target list, an index that is not a number, or `up` on the first item
- **Then** the command does not run: the first two answer `400` and `up` on the first item renders the page unchanged; a plain button's token posted with a move still answers `409`.

### DRAG-08: Dropped items run the targets command

- **Given** an Avalonia window with the behavior
- **When** the item data of `Drag.Format` is dropped on a target at the position of its second item
- **Then** the behavior raises `Drag.MovedEvent` with the target's command and `(key, target key, 1)` and, when no handler takes it, runs the command itself; a drop without the item data, or on a control that is not a target, runs nothing.

### DRAG-10: The kanban moves cards between lanes

- **Given** the Showcase kanban
- **When** it is rendered
- **Then** each lane's card list is a drop target keyed by the lane and each card is draggable by its id
- **When** a move to another lane at an index is posted
- **Then** the card is in that lane at that index, and when a card's "Move up" is posted to the plan engine (the lane's cards have a template per kind), then the card moves one place up in its lane.

## Drag script

### DRAG-04: Dragged items move at once and the server wins

- **Given** a page with two drop targets in the browser
- **When** an item is dragged with the pointer from the first list to the second position of the second
- **Then** the element is in its new place before the server answers, both lists are marked `aria-busy` and refuse another drag until the answer, the post carries `_btn` as the target's token, the index and the item key, and once the server answers, the lists show the server's order, which wins over the optimistic one.

### DRAG-05: A failed drop rolls the move back

- **Given** the same page
- **When** a drag is dropped and the server answers with an error
- **Then** the element goes back to its original place, the lists are unlocked, and the page is not reloaded.

### DRAG-06: The keyboard moves items with announcements

- **Given** the same page
- **When** an item is focused and Space picks it up, an arrow key moves it within its list or to the next list, and Space drops it
- **Then** each step is announced in an `aria-live` region and the move is posted like a pointer drop
- **When** Escape is pressed instead of the drop
- **Then** the item returns to its place, "cancelled" is announced and nothing is posted.

### DRAG-07: Without JavaScript move buttons post the form

- **Given** the browser with JavaScript disabled
- **When** an item's "Move down" button inside the `<noscript>` is pressed
- **Then** the browser posts the page's form natively with that button's `_btn` value.

### DRAG-11: Handles in table rows move their rows

- **Given** a drop target `data-drop="0" aria-label="Sections"` framing a `<table>` whose rows each hold a handle `data-drag` (Aioros.Cosmo's dashboard-01, its table frame the target and each row's handle the item)
- **When** the first row's handle is picked up with Space, moved down twice and dropped with Space
- **Then** its row moves within the table's body, the drop is announced as position 3 of 3 in Sections, and the post carries `_btn=0|2|a`; once the server answers, the rows show its order.

*Precedent: dnd-kit's `useSortable` on shadcn/ui's table rows (the row moves, the handle carries the listeners), DRAG-06.*

### DRAG-12: A lifted copy follows the pointer while the others make room

- **Given** two lists of draggable items in the browser
- **When** the visitor drags an item with the pointer
- **Then** a lifted copy of it (`data-aioros-drag-overlay`) follows the pointer above the page while the item itself stays dimmed where it would land: the copy is outside the form and holds no `name` or `id`, so the post carries each field once; the items making room for it move with an animation; when the item is dropped, the copy is gone once it reached its place, and the move posts as before (DRAG-04); with reduced motion asked for, nothing animates.

*Precedent: dnd-kit's `DragOverlay` and its sortable layout animations (shadcn/ui's kanban); SortableJS's fallback ghost.*

## Desktop drop

### DRAG-09: Dropped items run their command and revalidate

- **Given** the desktop target showing a page with a drop target
- **When** an item is dropped on it
- **Then** the navigator runs the target's command with the move and revalidates the page, as after a button's command.
