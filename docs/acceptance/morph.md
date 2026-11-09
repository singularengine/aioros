# Morph behaviors

Update animations declared in the markup a control writer or a library's slot renderer writes, run by the framework's script when its morph patches a marked element (`src/Targets/Aioros.Targets.Ssr/Documents/MorphScript.cs`, loaded by `Scripts.cs`): the element's geometry at rest as numbers (`data-aioros-morph`), how its attribute is rebuilt from them (`data-aioros-morph-path`), how the previous numbers correspond (`data-aioros-morph-map`), where new numbers start (`data-aioros-morph-from`) and the timing (`data-aioros-morph-duration`, `-delay`, `-easing`), the settings read from the element or its nearest ancestor that declares them. The app never writes these attributes or any script.

8 criteria. Each has an identifier and exactly one test.

### MORPH-01: A path moves from its previous points by proportional index

- **Given** a path declaring three points (`data-aioros-morph="0 100 50 50 100 80"`) rebuilt as `line monotone-x`, mapped by `ratio`, over 1600 ms linearly (settings on its group)
- **When** a post's answer gives it five points and another `d`
- **Then** right after the morph its `d` is d3's `curveMonotoneX` line through each new point's previous point at `Math.floor(index * 3 / 5)` (coincident points skipped as d3 skips them), 800 ms later the line through the points half way, and once the 1600 ms are over its `d` is the answer's own string; while it moves the path carries `data-aioros-morphing`, which a later morph of the same element keeps, and it is removed at the end.

### MORPH-02: New numbers start from their declared origin

- **Given** a kept path whose previous version declares an empty list (`data-aioros-morph=""`) and whose new one declares `data-aioros-morph-from="250 150"` with `line`
- **When** it is morphed
- **Then** every point starts from (250, 150)
- **When** a `rect` path appended by the morph (`data-aioros-morph="10 20 30 40"`, `data-aioros-morph-from="_ 60 _ 0"`, a `_` keeping the element's own number)
- **Then** its `d` starts as `M10,60h30v0h-30Z` and is `M10,40h30v20h-30Z` half way; an element put in place of another tag with an origin animates the same way; an appended element without an origin appears at once at its own geometry without `data-aioros-morphing`; and an appended element that animates from its origin finishes the CSS animations of itself and of the appended elements around it (the entrance the morph now plays), while an appended element that does not animate keeps its own.

### MORPH-03: Attributes move by index after their delay and easing

- **Given** circles declaring `data-aioros-morph-path="cx cy"` mapped by index (the default) with a 160 ms delay, a 1600 ms duration and `cubic-bezier(0.25, 0.1, 0.25, 1)`, and a third circle the previous render did not have
- **When** they are morphed
- **Then** at 160 ms each circle is still at its previous center, at 960 ms its center has moved by 0.8024 of the change (CSS's `ease` at half its time), the new circle stays at its own center, and the named easings `linear`, `ease-in`, `ease-out` and `ease-in-out` give their CSS curves (`ease-in-out` at half time: 0.5).

### MORPH-04: Sectors polygons bands and glyphs are rebuilt half way

- **Given** a `sector` (center, inner and outer radius, start and end angle in degrees), a `polygon`, a `band monotone-y` (each point with its base point) and a `glyph` (center and area)
- **When** each is morphed to new numbers
- **Then** half way the sector's `d` is Recharts' `getSectorPath` for the angles half way (a large arc past 180 degrees, a full turn capped at 359.999, a wedge to the center without an inner radius), the polygon's is the closed line through the points half way, the band's is d3's `area` with `curveMonotoneY` (its points forward, its base points back, closed), and the glyph's `transform` moves and scales its own shape to the center and the area half way (`matrix(s,0,0,s,x-s*x0,y-s*y0)`, s the square root of the area over its own).

### MORPH-05: A morph restarts from where it is and honours reduced motion

- **Given** a path half way through a morph
- **When** another answer gives it new numbers
- **Then** it starts again from where it is (its numbers half way) towards the new ones
- **When** an answer gives it the numbers it is already moving to
- **Then** it carries on undisturbed
- **When** an answer gives an element the numbers and `data-aioros-morph-key` it already has
- **Then** nothing moves and no `data-aioros-morphing` is set, while the same numbers with another key run the morph
- **When** the reader asked for reduced motion (`prefers-reduced-motion: reduce`)
- **Then** every element takes its new geometry at once.

### MORPH-06: The morph module loads only where pages declare it

- **Given** a page whose markup declares no morph behavior
- **When** it loads and posts
- **Then** the framework's morph module is never requested
- **When** a page that declares one, when it loads, or when a client navigation leads to it
- **Then** the script requests `/_aioros/morph/{hash}.js` once, answered as `text/javascript` with `Cache-Control: public, max-age=31536000, immutable`, and another name under `/_aioros/morph/` answers `404`.

### MORPH-07: Appended morphs recalculate styles once

- **Given** a client navigation to a page whose answer the script appends 200 elements to, each declaring `data-aioros-morph-from` and a CSS enter animation
- **When** the morph shows them in Chromium
- **Then** each starts its morph and has its CSS animations finished as before, and the whole navigation recalculates styles a bounded number of times (fewer than 20, Chromium's `RecalcStyleCount`) instead of once per element: the morph module starts the appended elements after the script has placed them all, then reads their animations once and finishes them together, so no read of computed style falls between two writes.

*Precedent: the read/write batching of fastdom and of React's commit phase, which keep layout and style reads from interleaving with DOM writes (layout thrashing). Measured on Pictor's Gallery: `/custom` → `/interactions` 2.5 s with 210 style recalculations, 0.09 s with 6.*

### MORPH-08: Shapes appended with their series play their enter animation

- **Given** a morph that appends a group declaring `data-aioros-morph-path` together with its shapes, each declaring `data-aioros-morph-from` and a CSS enter animation (a client navigation to a page with a chart, or a new series in a kept chart)
- **When** the shapes are shown
- **Then** none of them morphs from its origin: none carries `data-aioros-morphing`, each keeps its own `d` and its CSS animation keeps running, so the chart plays its enter animation as a newly mounted Recharts series does (a `Line` draws from its first point, never from Recharts' `animateNewValues` origin)
- **When** the same shape appended into a kept group declaring `data-aioros-morph-path` (a new item of a kept series)
- **Then** it still morphs from its origin and its CSS animation is finished (MORPH-02).

*Precedent: Recharts animates from `prevPoints` only in a series that was rendered before; a newly mounted series has none and plays its enter animation.*
