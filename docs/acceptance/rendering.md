# Rendering

Render plans and their UTF-8 writer (`src/Aioros.Runtime/Rendering`). A plan is static HTML plus slots bound to view-model paths; rendering it touches no UI framework, so it runs on any thread.

31 criteria. Each has an identifier and exactly one test.

## Render plan

### RENDERING-01: Literals are written as utf 8

- **Given** a plan of literals with non-ASCII text
- **When** it is rendered
- **Then** the output is the same text encoded as UTF-8.

### RENDERING-02: Values are HTML encoded

- **Given** a value slot whose value contains `&`, `<`, `>` and `"`
- **When** it is rendered
- **Then** those characters are written as HTML entities and every other character is kept.

### RENDERING-03: Values are formatted with the current culture

- **Given** a value slot
- **When** the value is null
- **Then** nothing is written
- **When** it is formattable
- **Then** it is written with the current culture, as Avalonia bindings format it.

### RENDERING-04: Raw values are not encoded

- **Given** a raw value slot (markup such as an icon)
- **When** it is rendered
- **Then** the value is written without encoding.

### RENDERING-05: Conditions render their body only when true

- **Given** a condition slot
- **When** its value is `true`
- **Then** its body is rendered
- **When** it is `false` or not a boolean
- **Then** nothing is written.

### RENDERING-06: Repeats render their body per item

- **Given** a repeat slot over a list
- **When** it is rendered
- **Then** its body is written once per item, with the item as the scope of the body's slots.

### RENDERING-07: IDs are numbered in document order

- **Given** id slots inside and around a repeat
- **When** the plan is rendered
- **Then** they are numbered from 0 in document order.

### RENDERING-08: Selects mark the selected option

- **Given** a select slot
- **When** it is rendered
- **Then** one `<option>` is written per option, encoded, and only the option equal to the value is marked `selected`.

### RENDERING-09: Bodies render without head and tail

- **Given** a plan with a head, a body and a tail
- **When** only the body is rendered
- **Then** the head and the tail are not written.

### RENDERING-10: Builders merge consecutive literals

- **Given** a plan builder that receives consecutive literals around nested scopes
- **When** the plan is built
- **Then** each run of consecutive literals is one literal node, also inside the scopes.

### RENDERING-11: Scopes read from another model

- **Given** a scope slot that names another model of the render
- **When** it is rendered
- **Then** its body's slots read from that model (a layout's outlet renders the page's model).

### RENDERING-12: Plans know whether they have fields

- **Given** plans with and without form fields (ids or choice groups), also inside conditions, repeats, scopes and the cases of a type switch
- **When** they are inspected
- **Then** only those with fields report that they have fields.

## Plan diff

### RENDERING-13: Templates and dynamics join into the rendered HTML

- **Given** a plan's body
- **When** its template and its dynamics for a model are taken apart
- **Then** the template is JSON of the statics between slots (`s`) with the nested templates of conditions (`c`) and repeats (`r`) by slot index, the dynamics are a JSON array with each slot's HTML (values encoded, field ids numbered as in a full render), `0` or the nested array for a condition and one array per item for a repeat, and joining them gives exactly the HTML the plan renders for that model; the template's fingerprint is the same for any model and changes when the statics change.

*Precedent: Phoenix LiveView's rendered statics (`s`) and dynamics.*

## Current link

### RENDERING-14: Links to the current page are marked

- **Given** links in a plan (a literal `href` or one bound to the model)
- **When** the plan is rendered for a request path
- **Then**
  - A link whose path equals the request path, or is its prefix up to a `/` (`/games` for `/games/12`), is marked `aria-current="page"`
  - `/` and a link marked exact (`a:Web.Exact`) are marked only on their own path, a link with a query is marked only when the request has each of its parameters with the same value (so `/games` stays current on `/games?genre=RPG`, `/crm?page=2` is not current on `/crm`), the fragment is ignored, nothing is marked when the render has no request path, and the marks survive the plans file; every such link carries `data-nav` (and `data-exact` when exact), so the browser can mark it again after a navigation that keeps the layout.

*Precedent: React Router `NavLink` (`aria-current="page"`, segment-boundary matching, `end`) and TanStack Router `Link` (search parameters must match, `exact`).*

## View node

### RENDERING-15: Views render their own model isolated from the host

- **Given** a view slot that names a model of the render and an instance key
- **When** it is rendered
- **Then** its body is written inside `<div data-aioros-view="key" style="display:contents">` reading that model, and a slot inside it that reads an outer scope (`^1`) reads nothing of the host's scopes
- **When** the model is the hidden marker
- **Then** nothing is written
- **When** it is the failed marker
- **Then** the slot's error body is written inside `<div data-aioros-view="key" data-view-error role="alert" style="display:contents">`.

### RENDERING-16: Views are serialized scanned and one dynamic slot

- **Given** a plan with a view slot
- **When** it is serialized and read back
- **Then** it renders the same
- **When** it is inspected
- **Then** it has fields when its body has them
- **When** its template and dynamics are taken apart
- **Then** the view's whole region is one slot of the dynamics (RENDERING-13).

### RENDERING-17: Field IDs are namespaced per view and a region renders alone

- **Given** a plan with form fields (ids, a radio group sharing its leader's id, a choice group) in the page before, between and after view slots, inside a view slot and inside a view nested in it
- **When** it is rendered
- **Then** the page's ids number from 0 in document order as if the views were absent, each view's ids are written `{slot}.{n}` numbered from 0 inside it (`name="f2.0"`), a nested view's in its own namespace, and the field log records each field's view
- **When** one view's region is rendered alone (`RenderView`)
- **Then** it writes the same bytes as that region inside the page, and a view inside a condition that is false, or a slot that names no view, is not found.

*Precedent: Blazor's `FormName` (one handler per named form), LiveView component ids.*

### RENDERING-18: Repeated views render each items model in its own region

- **Given** a plan whose repeat writes a repeated view slot for each item, and that slot holding the item models of a list keyed by the items' keys (one model, one hidden, one failed)
- **When** it is rendered
- **Then**
  - Each item's region is `<div data-aioros-view="{key}[{item}]" style="display:contents">` around its body reading that item's model, the hidden item writes nothing and the failed one its error region; the item's fields are named `{slot}-{item}.{n}`, numbered from 0 inside each item with the item key escaped (no `.`, `-` or `|`), and the field log records each field's slot and item
  - `RenderView` with the slot and an item key writes that item's region alone, the same bytes as inside the page, and finds nothing for a key that is not in the list; the plan serializes and reads back the same.

### RENDERING-19: A changed field picks the view a post binds

- **Given** a post without a button that names a changed field in `_chg` (the framework script's post of a changed selection)
- **When** the view it belongs to is read
- **Then** it is the field token's view (`2.0` is slot 2, `1-b%2D2.3` slot 1 item `b-2`, `4` the page), a post naming neither a button nor a change is the page's, a button's token wins over a changed field's, and a malformed token is refused (no view, a bad request).

## Writer node

### RENDERING-20: A registered writer writes each render from the bound value after a round trip

- **Given** a writer node whose writer a library registered (`SlotWriters.Register`, the entry the app's generated code hands the runtime) and whose source is a bound path
- **When** the plan is serialized and read back through the runtime's resolver (`PlanSlotResolver`, no Avalonia)
- **Then** the writer is created once from the argument the plan recorded, with a member lookup that reads the bound data through the generated accessors, and each render writes that writer's UTF-8 output for the value the path reads on that render, byte for byte as the original node does.

*Precedent: Lit's and Solid's compiled holes filled per render; React server components rendering a component's markup on the server from its props.*

### RENDERING-21: A writer with no entry fails to load naming it

- **Given** a plans file whose writer node names a writer no registry entry has
- **When** it is read
- **Then** reading fails with an error that names the writer and says that its library declares it with `SlotWriterAttribute<T>`.

### RENDERING-22: Parts of a renderer are read posted and run after a round trip

- **Given** a writer node of a registered slot renderer with parts (a read of a bound path, a number field on a bound path, and a command on a bound command path)
- **When** the plan is serialized and read back through the runtime's resolver (`PlanSlotResolver`, no Avalonia)
- **Then** each render writes the same bytes as the original node, the renderer finds each read's value under its key and none under an undeclared key, and the field log of a render records, in order, the field with its number description and one command field per button with its parameter, so a post through the plan binds the field and runs the command.

### RENDERING-23: A field or command under an undeclared key fails the render naming it

- **Given** a writer node whose renderer writes a field or a command under a key its node declares as neither
- **When** it is rendered
- **Then** rendering throws an error that names the key.

### RENDERING-24: Choices and composites are written and posted after a round trip

- **Given** writer nodes of a registered slot renderer with a choice part on a bound text path and a composite part over two number fields on bound paths, the renderer's entry carrying its decoder
- **When** the plan is serialized and read back through the runtime's resolver (`PlanSlotResolver`, no Avalonia)
- **Then** each render writes the same bytes as the original nodes, every option of the choice carries the field's one id, and the field log records the choice with its options' values and the composite with its members' descriptions; a post through the plan binds a choice value an option was written with and ignores one that none was, and decodes a composite post through the entry's decoder into both members, clamped as their number descriptions say; a renderer that writes an option under a key not declared as a choice, or a plain field under a choice's key, fails the render naming the key.

## Plan allocation

### RENDERING-25: A plan builds its template only when it is read

- **Given** a body of more than 85 KB of literals (the large object heap's threshold) around a value and a repeat
- **When** a plan is created from it, as the engine creates one for every literal render and every post it binds
- **Then** creating it allocates less than a tenth of the body's size: its template (the statics and the fingerprint) is built the first time it is read, and is then the same JSON and fingerprint as a template read at once from a plan of the same body; two plans of the same body are equal whether or not either template was read.

*Precedent: LiveView sends a template's statics only to a client that lacks them, so a render that no client asks the statics of never builds them.*

### RENDERING-26: Dynamics grow no array with the page

- **Given** a plan whose slots write more than 85 KB in all (3,000 rows of values and a value of 100 KB)
- **When** its dynamics are written a second time into a pooled buffer
- **Then** writing them allocates less than a quarter of their size: each slot's value is written into a pooled scratch buffer reused from its start for every slot, so no array grows with the page, and the dynamics are the same JSON as before.

*Precedent: System.Text.Json and Kestrel write through buffers rented from `ArrayPool<byte>.Shared`.*

### RENDERING-27: A literal alone between slots is kept as it is

- **Given** a plan builder given a literal of more than 85 KB alone between two slots, as the engine's recorder gets a slot writer's markup before its first field
- **When** the plan is built
- **Then** building allocates less than a tenth of the literal's size: the literal node holds the text it was given (no copy through a string builder), and a literal node encodes its UTF-8 bytes on its first write, so a plan built only for its fields (the engine's binding of a post) never encodes them; literals given next to each other still make one node, unless a literal is given apart (as the engine's recorder gives a slot writer's markup of 16K characters or more in a render it keeps no plan from), which becomes a node of its own after the text before it, holding the text it was given.

*Precedent: `string.Concat` returns its only non-empty argument as it is.*

## Slot writer

### RENDERING-28: Keyed styles are collected once per page on both paths

- **Given** a page with three slot renderers that each contribute the CSS rule set keyed `sync-a` (`SlotOutput.WriteStyle(key, css)`, asking `SlotOutput.NeedsStyle(key)` first) and one that contributes `sync-b`
- **When** the page is rendered through the plan and through the engine (whose recorder keeps a renderer's contribution as a node of the plan)
- **Then** each path collects each key once, the first contribution in document order winning, in the order the keys were first contributed, and the same on both paths (`VerifiedPlans` compares them as it compares the bodies); none of the rule sets is written in the body, a renderer asking for a key already contributed is told it is not needed, and a key with a character other than letters, digits, `-`, `_` and `.` is refused.

*Precedent: styled-components' and Emotion's style registries (a rule set inserted once per document under its hash or key, however many components use it).*

## Indexed path

### RENDERING-29: Indexed member paths read and write the element at their index

- **Given** member paths with integer indexes (`Items[1].Name` on a `List<T>`, an array, an `ImmutableArray<T>` and a type that is only an `IReadOnlyList<T>`, `Grid[0][1]`, `^1.Items[0].Name` from an outer scope, and `Items[1]` as the last member)
- **When** they are read and written through the runtime's member paths
- **Then** each reads the element at its index (lists through `IList`, a read-only list through its `[n]` accessor), an index out of range or a value that is not a list reads null, a setter writes the element's member (or the element itself) and writes nothing out of range, and the paths read with generated accessors and with runtime delegates only give the same values.

### RENDERING-30: Indexed plans read the same through generated accessors and runtime delegates

- **Given** a plan whose slots read `Items[1].Name` and `Ranking[0].Name` (a read-only list) written to a plans file
- **When** it is read back with generated accessors and with runtime delegates only and both are compared for a model as `aioros build` compares them (`PlanComparison.FirstDifference`)
- **Then** they write the same bytes, and no index of a list is looked up as a member (none is counted in the accessor coverage), since lists are read through `IList`.

## Plans file

### RENDERING-31: Planned pages keep their culture

- **Given** precompiled pages for the cultures `en-US` and `fr-FR` (`PlannedPage.Culture`)
- **When** the plans file is written and read back
- **Then** every page keeps its culture; a page planned without a culture (an app that does not localize) is written without one, and a file written before cultures reads as pages without a culture.
