# Pointer

The pointer's position declared in the markup a control writer or a library's slot renderer writes, run by a module of the framework's script (`src/Targets/Aioros.Targets.Ssr/Documents/PointerScript.cs`, loaded by `Scripts.cs` only where a page declares it): an element marked `data-aioros-pointer` carries the mouse's position as CSS custom properties, so a library places what follows the mouse (a chart's tooltip box) with CSS alone. The app never writes these attributes or any script; touch is covered by the focus that follows the finger (`data-aioros-follow`, [focus.md](focus.md)).

4 criteria. Each has an identifier and exactly one test.

### POINTER-01: The mouse position is written once per frame and removed on leave

- **Given** an element marked `data-aioros-pointer` in a page taller than the window
- **When** the mouse moves 60, 40 px inside its box and then 150, 120 px inside it
- **Then** after each animation frame its inline style holds `--aioros-pointer-x` and `--aioros-pointer-y` in px relative to its box (`60px`, `40px`, then `150px`, `120px`), read through one `getBoundingClientRect` for the whole movement
- **When** three moves arrive before one frame
- **Then** the frame writes each property once, with the last point
- **When** the page scrolls by 50 px and the mouse moves to 150, 150 px inside the box where it now is
- **Then** the box is read once more and the properties hold `150px` and `150px`
- **When** a mouse comes over the element without moving (the page scrolled under it, which Firefox reports with `pointerover` alone)
- **Then** the properties are set from that event
- **When** the mouse leaves the element
- **Then** both properties are removed, and a move already waiting for its frame writes nothing; a `touch` pointer moving over it sets nothing, and a mouse over an element without the attribute sets nothing.

### POINTER-02: An SVG can ask for its view box units

- **Given** an `<svg>` marked `data-aioros-pointer="viewbox"` with `viewBox="0 0 100 50"` drawn 400 by 200 px
- **When** the mouse moves to 200, 100 px inside its box
- **Then** the properties hold its viewBox units, `50px` and `25px` (a CSS length in an SVG's user space counts one px per unit)
- **When** the same view box drawn 400 by 100 px with the default `preserveAspectRatio` (`xMidYMid meet`: the drawing scaled by 2 and centred), when the mouse moves to 200, 50 px inside its box
- **Then** they hold `50px` and `25px` too.

### POINTER-03: A morph keeps the pointer properties

- **Given** the mouse resting over an element marked `data-aioros-pointer` whose properties are set
- **When** the framework's morph patches the element with the server's new markup (another attribute changed, no inline style in the markup)
- **Then** the properties keep their values without the mouse moving.

### POINTER-04: The pointer module loads only where pages declare it

- **Given** a page whose markup declares no `data-aioros-pointer`
- **When** it loads and posts
- **Then** the framework's pointer module is never requested
- **When** a page that declares one, when it loads, or when a client navigation leads to it
- **Then** the script requests `/_aioros/pointer/{hash}.js` once, answered as `text/javascript` with `Cache-Control: public, max-age=31536000, immutable`, and another name under `/_aioros/pointer/` answers `404`.
