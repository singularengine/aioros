# Overlays

Overlay behaviors declared in the markup the control writers write (`data-aioros-overlay`), run by a module of the framework's script (`src/Targets/Aioros.Targets.Ssr/Documents/OverlayScript.cs`, loaded by `Scripts.cs` only where a page declares it), and the prompts and notices the framework's script shows (`Scripts.cs`). The behaviors follow the WAI-ARIA Authoring Practices (menu, menubar, tooltip, dialog patterns) and Radix's primitives where the APG leaves a choice; the popover API and the `<dialog>` element give the rest (light dismiss, Escape, the top layer, the focus given back). The app never writes these attributes or any script.

19 criteria. Each has an identifier and exactly one test.

## Overlay script

### OVERLAY-01: The overlay module loads only where pages declare it

- **Given** a page without `data-aioros-overlay` and a page with a menu popover marked so
- **When** each is opened (directly and by a client navigation) and a post answers the first
- **Then** only the second loads the overlay module `/_aioros/overlay/{hash}.js`, served as `text/javascript` and cached `immutable`, and an unknown hash is 404.

### OVERLAY-02: Menus move the focus with the keyboard

- **Given** a menu popover `role="menu"` of items (`role="menuitem"`, one `disabled`, one `menuitemcheckbox`) opened by its trigger
- **When** it opens
- **Then**
  - The first item has the focus
  - ArrowDown and ArrowUp move it without wrapping and skip the disabled item, Home and End go to the ends, a typed letter goes to the next item whose text starts with it (two letters typed within a second to the item starting with both), and ArrowDown on the trigger of a closed menu opens it
  - Escape closes it and gives the focus back to the trigger.

### OVERLAY-03: Sub menus open with the pointer and the arrows

- **Given** a menu with an item that opens a sub-menu (`aria-haspopup="menu"`)
- **When** the mouse moves over an item
- **Then** the item takes the focus
- **When** the mouse rests on the sub-menu's item for 100 ms
- **Then**
  - The sub-menu opens with the focus left on its item, and moving to another item of the menu closes it
  - ArrowRight on the item opens the sub-menu with the focus on its first item, and ArrowLeft there closes it and gives the focus back to its item.

### OVERLAY-04: Menu bars move between their menus

- **Given** a menu bar (`role="menubar"`) of two items that open menus
- **When** ArrowRight and ArrowLeft are pressed on a bar item
- **Then**
  - The focus moves to the next or previous bar item (wrapping)
  - ArrowDown opens the item's menu
  - ArrowRight in an open menu closes it and opens the next bar item's menu.

### OVERLAY-05: Context menus open at the pointer

- **Given** an area marked `data-aioros-context` with a context menu popover (`class="menu cm"`)
- **When** it is right-clicked
- **Then**
  - The browser's own menu is prevented and the popover opens at the pointer (its `--aioros-x` and `--aioros-y`), moved left or up to stay in the view near the right or bottom edge, with the focus on its first item
  - Shift+F10 on the focused area opens it at the area's bottom-left corner; a right click inside the open menu opens nothing else.

### OVERLAY-06: Hover cards open and close after their delays

- **Given** a control wrapped in `data-aioros-hover` with `data-open="700"` and `data-close="300"` and its `popover="hint"` card
- **When** the mouse rests on the control
- **Then**
  - The card opens after 700 ms and not before; moving into the card keeps it open; leaving both closes it 300 ms later
  - Tab focus on the control (`:focus-visible`) opens it after the delay and moving the focus away closes it
  - Escape closes it.

### OVERLAY-07: Clicked flyouts toggle with their control

- **Given** a control wrapped in `data-aioros-click` with its popover
- **When** the control is clicked
- **Then** the popover opens, and a click on the control while it is open closes it (the light dismiss on the press does not let the click reopen it); a click inside the popover leaves it open.

### OVERLAY-08: Tool tips hide on escape and skip their delay

- **Given** two controls with tool tips (`.tip` with a 400 ms `--tip-delay` and `data-between="300"`)
- **When** the mouse rests on the first
- **Then**
  - Its tip shows after the delay
  - Escape hides it while the pointer stays; leaving it and resting on the second within 300 ms shows the second's tip at once, and after more than 300 ms with no tip shown the delay applies again; a click's focus does not show a tip and Tab's focus does.

### OVERLAY-09: Hover triggers open their popovers after their delays

- **Given** a row of two buttons with `popovertarget`, `data-hover-open="200"` and `data-hover-close="150"`, each followed by its popover (Radix's NavigationMenu triggers)
- **When** the mouse rests on the first
- **Then**
  - Its popover opens after 200 ms and not before; moving into the popover keeps it open; moving onto the second button while the first is open opens the second at once and closes the first; leaving the second button and its popover closes it 150 ms later and not before; a click on a button whose popover hovering opened closes it and it does not reopen while the pointer stays
  - Tab's focus on a button opens nothing.

### OVERLAY-15: Switching popovers carry their motion

- **Given** a row of three buttons opened on hover (`data-hover-open`), the first two marked `data-switch`, each followed by its popover
- **When** the mouse opens the first and then moves onto the second
- **Then** the second popover opens with `data-motion="from-end"` and the first closes with `data-motion="to-start"`; moving back onto the first opens it with `from-start` and closes the second with `to-end`; a click on the second while the first is open does the same as the pointer; an opening that replaces nothing (after the row closed) carries no `data-motion`, and the third button, unmarked, never sets one.

*Precedent: Radix NavigationMenu, which sets `data-motion` on the content it shows and the one it hides from their items' order.*

## Modal

### OVERLAY-10: Marked dialogs are written closed for the browser

- **Given** a library's dialog marked `a:Web.Modal="IsOpen"` (Aioros.Cosmo's `c:Dialog`) whose `IsOpen` is bound two way to the model's `Open`, closed, its mask and surface (a title, a description, a field bound to `Name`, a `Save` button bound to a command and a `Close` button marked `a:Web.Closes`) marked `a:Web.ModalOf` it and its light dismiss, a button marked `a:Web.Opens` it, and a plain button after them
- **When** the page is planned and rendered for a closed and an open model
- **Then**
  - The plan is reusable and writes what Avalonia renders; the dialog is written once, after the page, as `<dialog id="dlg1" class="prompt" data-aioros-modal="_s0" data-aioros-dismiss data-change …>` (the name of the hidden input that carries its state, `_m0`), `open` only while the model's flag is, labelled by its title and described by its description, holding its mask, its surface with the field and buttons, and a hidden check box of the flag; the trigger is `<button type="button" commandfor="dlg1" command="show-modal">` with its hot key (`data-aioros-key`), not a field of the form (the plain button keeps `value="0|"`)
  - `Close` is `commandfor="dlg1" command="close"`; the page writes no prompt for it and no `_prompt` replay; the closed dialog is written from the overlay the library shows while Aioros sets `a:Web.Shows` on its owner, so the model's `Open` is never written while the page is written; and the same page with an unmarked dialog writes, before and after a post opens it, exactly what it wrote before (ENGINE-213, 215).

### OVERLAY-11: A post from an open dialog finds its buttons

- **Given** the page of OVERLAY-10
- **When** a post carries the dialog's state open (`_m0=True`, as the browser left it) and presses `Save`
- **Then** the state is restored before the button is looked for, `Save`'s command runs and the answer writes the dialog closed (the command closed it)
- **When** the dialog was opened by a post pressing a button bound to the model's `Show` command
- **Then** that answer writes it open in the same `<dialog>`, no prompt dialog beside it, with the `_prompt` replay of the step that opened it (ENGINE-213), and the next post from inside it, carrying both, finds `Save`: each step restores the posted state before it binds, the replay then re-runs the opening step so a command waiting on the dialog resumes with its answer, and the pressed button's command decides what the answer shows; a state that does not parse is left out and a post naming a button the restored state does not show answers its 409 with the page rendered afresh (need 73's rule); and a post that carries the check box with `_chg` binds the model's flag.

### OVERLAY-14: Only the open dialogs controls resume it without the script

- **Given** the page of OVERLAY-10 whose dialog a post opened through the model's `Show` command (OVERLAY-11: the answer writes it open with the `_prompt` replay of that step and no prompt dialog)
- **When** the answer is written
- **Then**
  - Beside its `_prompt` field it carries a hidden `_answers` field naming the tokens of the controls inside the open dialog (its fields and buttons, the dialog's own posted check box among them); and when the answer's form is posted as a browser without scripting posts it (the dialog's state, its fields, `_prompt` and `_answers`), `Save` inside the dialog runs through the replayed step, while `Plain`, a button of the page outside the dialog, runs alone, the step not replayed.
  - With the script the dialog is modal and the page behind it inert, so only the dialog's controls post.

### OVERLAY-18: A keyed dialog carries its key

- **Given** an overlay owner marked `a:Web.Modal` with no trigger whose `a:Web.ModalKey` is Ctrl+K (Aioros.Cosmo's `c:CommandDialog` without a `Trigger`), and one without a key
- **When** the page is written
- **Then** the first owner's `<dialog>` carries the key as a shortcut does (`data-aioros-key="ctrl+KeyK"` and `aria-keyshortcuts="Control+K"`) and the second carries none.

*Precedent: cmdk's dialog, which shadcn/ui's `CommandDialog` opens on a document `keydown` of ⌘K or Ctrl+K with no trigger; Avalonia's `KeyGesture`.*

## Modal script

### OVERLAY-12: Marked dialogs open and close in the browser

- **Given** a page holding a dialog marked `data-aioros-modal` and `data-change`, closed, with its state input, its check box, its trigger, its `Close` button and its mask
- **When** the trigger is clicked
- **Then** with no request but the change it posts the dialog opens as a modal (`:modal`), its state input reads `True`, its check box is checked and the focus is inside it
- **When** `Close` is clicked
- **Then**
  - It closes, the focus returns to the trigger and its state reads `False`
  - Escape and a click on the mask close it as well, and without `data-aioros-dismiss` the mask does not; the closed dialog's content is not in the accessibility tree; an answer that writes it open shows it with `showModal` (never as a non-modal dialog) and one that writes it closed closes it; and a dialog without `data-change` posts nothing when it opens or closes.

### OVERLAY-13: A dialog whose surface slides in keeps the focus on its field

- **Given** a closed marked dialog whose content's box scrolls (`overflow:auto`) and whose surface slides in from its own width (a sheet's entrance)
- **When** its trigger opens it in Chromium
- **Then** its content is enabled before the dialog opens, so the browser focuses its first field, which keeps the focus once the slide has ended; the content's box, scrollable only while the surface slides, never takes the focus and drops it to the page.

### OVERLAY-19: A keyed dialog opens and closes with its key

- **Given** a page with a closed `dialog[data-aioros-modal]` carrying `data-aioros-key="ctrl+KeyK"` and a text field
- **When** the visitor presses Ctrl+K with the focus in the text field
- **Then** the dialog opens modal with no request and its state input reads `True`
- **When** Ctrl+K (or Meta+K, which stands for Ctrl) is pressed again from its own field
- **Then** it closes with no request and the state input reads `False`; a button with the same key would still be clicked as before.

*Precedent: cmdk's toggle on ⌘K; HTML's `showModal` and `close`.*

## Notice deck

### OVERLAY-16: A deck writes its stack

- **Given** a notification manager whose panel is a deck (`a:Web.Deck` 14, `a:Web.DeckVisible` 3, `a:Web.DeckScale` 0.05, `a:Web.DeckBehind` `behind`, anchored at the bottom) and whose card theme fades its content presenter to opacity 0 with the `behind` class and moves its root over 400 ms
- **When** a post shows four cards
- **Then** the layer is a `notice na nd` popover marked `data-aioros-deck="14 3 0.05"` and `data-aioros-anchor="bottom"`, as wide as the panel and as tall as the newest card; the cards are written oldest first, each with its height in `data-h`, the newest at `translate:0 0px`, each behind it `14 px` further up per place with its scale `0.95`, `0.9` and `0.85`, the newest's height and `data-behind`, the fourth at opacity 0, the newest on top (`z-index`), all moving over a class with the card's 400 ms transform transition on `translate`, `scale`, `opacity` and `height`; the content presenter carries a class whose rules give it opacity 1 in a card not behind and 0 in a card `data-behind`; and with `a:Web.DeckExpanded` the deck is marked `data-aioros-expanded`, its cards stand 14 px apart at their own heights at scale 1 with no `data-behind`, and it is as tall as its three newest cards and their gaps.

*Precedent: sonner 2.0.7's toaster (`--lift-amount`, `--toasts-before`, `--front-toast-height`, `data-expanded`, `visibleToasts`).*

## Swipe mark

### OVERLAY-17: Swiped elements and closing buttons are marked

- **Given** a `Border` with `a:Web.Swipe` set to `Down, Right, Sideways`, one with `Up` and `a:Web.SwipeDistance` 0.25 and `a:Web.SwipeVelocity` 0.4, one with only `Sideways`, and a button with `a:Web.Closes` outside any library dialog
- **When** the page is rendered
- **Then** the first is marked `data-aioros-swipe="down right"` and `data-aioros-swipe-by="45 0.11"` (sonner's threshold and velocity by default), the second `data-aioros-swipe="up"` and `data-aioros-swipe-by="0.25 0.4"` (vaul's), the third carries no swipe mark, and the button is a `type="button"` marked `data-aioros-closes`, which posts nothing.
