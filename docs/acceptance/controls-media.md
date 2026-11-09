# Controls: media

Images, icons and shapes (`src/Adapters/Aioros.Avalonia/Html/Families/MediaFamily.cs`).

24 criteria. Each has an identifier and exactly one test.

### MEDIA-01: Canvases are SVG

- **Given** a `Canvas` of shapes
- **When** it is rendered
- **Then** it is an `<svg>` with the canvas size as `width` and `height` and no view box, so its user units are CSS pixels as in Avalonia (a canvas never scales its children), with an element per visible shape, with its fill and stroke; a canvas without a background lets the pointer through (`pointer-events:none`), as Avalonia hit-tests no panel without one (an empty overlay dialog host over a page), while one with a background keeps it.

### MEDIA-02: Paths are SVG paths

- **Given** a `Path` with fill, stroke, thickness, dash array, line cap and join
- **When** it is rendered
- **Then** it is an `<svg>` holding a `<path>` whose `d` is the geometry's path data, with `fill`, `stroke`, `stroke-width`, `stroke-dasharray` (the dashes times the thickness), `stroke-linecap`, `stroke-linejoin` and its fill rule; with no stretch the view box runs from the origin to the geometry's far corner, and with `Stretch="Uniform"` and a size the view box is the geometry bounds, scaled with `preserveAspectRatio="xMinYMin meet"`.

### MEDIA-03: Path icons are inline SVG

- **Given** a `PathIcon`
- **When** it is rendered
- **Then** it is an inline `<svg>` of the theme icon size whose view box is the geometry bounds, filled with `currentColor`, with a `color` only when its foreground is set on it.

### MEDIA-04: Rectangles and ellipses are boxes

- **Given** a `Rectangle` and an `Ellipse` outside a `Canvas`
- **When** they are rendered
- **Then** each is a box with its size, its fill as background and its stroke as a border; the rectangle's corner radius is `RadiusX` plus half the stroke, the ellipse's is `50%`.

### MEDIA-05: Shapes outside canvases are SVG

- **Given** a `Line`, a `Polyline`, a `Polygon`, an `Arc` and a `Sector` outside a `Canvas`
- **When** they are rendered
- **Then** each is an `<svg>` with a `<path>` of its geometry (the arc and the sector laid out at their size), with its fill and stroke.

### MEDIA-06: Images are data uri imgs

- **Given** an `Image` of an `avares://` asset with `AutomationProperties.Name`, a width and `Stretch="UniformToFill"`
- **When** it is rendered
- **Then** it is an `<img>` whose source is the bitmap as a PNG data URI, whose `alt` is the name, whose `width` and `height` are the bitmap size, and whose style has the width, `height:auto` and `object-fit:cover`.

### MEDIA-07: Viewboxes scale their child

- **Given** a `Viewbox` with a size and a child
- **When** it is rendered
- **Then** it is an `<svg>` whose view box is the child's size as Avalonia measures it, scaled by `preserveAspectRatio` from its `Stretch`, with the child inside a `<foreignObject>`.

### MEDIA-08: Icons draw natively

- **Given** an `Icon` rendered by Avalonia itself (desktop, mobile, the compatibility bench)
- **When** it is drawn
- **Then** its glyph appears at its size with its foreground and stroke width, from the same paths the web writes; an unknown kind draws nothing.

### MEDIA-09: Images from URLs are lazy imgs

- **Given** an Avalonia `Image` whose source is the URL in AsyncImageLoader's `ImageLoader.Source` (the Avalonia community's loader for remote images) and whose `AutomationProperties.Name` is its description, both bound in an item template
- **When** the page is compiled and rendered for other items
- **Then** each item is an `<img>` with that item's `src` and `alt` (encoded), `loading="lazy"` and `decoding="async"`, filling its box (`width:100%` when no width is set) with `object-fit` from `Stretch` (`cover` for `UniformToFill`, `contain` for `Uniform`, `fill` for `Fill`); the plan stays reusable.

*Precedent: Next.js `<Image>`, lazy by default.*

### MEDIA-10: Icons take their stroke look from their class

- **Given** an `Icon` with a kind, a size, a stroke width and a foreground
- **When** it is rendered on the web
- **Then** it is an inline `<svg>` with the 24-unit view box, `aria-hidden="true"` and its size as `width` and `height`, holding the kind's glyph, and its stroke look (`fill:none`, `stroke:currentColor`, its stroke width, round caps and joins) and foreground are declarations of its class in the style sheet, written once for every icon of that look, not attributes repeated on each `<svg>`.

### MEDIA-11: Canvas children are placed as the canvas arranges them

- **Given** a `Canvas` with a `Rectangle` without `Canvas.Left` and `Canvas.Top`, one with both, one with `Canvas.Right` and `Canvas.Bottom` and one with `Canvas.Left` and `Canvas.Bottom`
- **When** it is rendered
- **Then** each is placed where the canvas arranges it, by `transform="translate(x y)"` (none at the origin): an unset side is 0, `Right` and `Bottom` place the child at the canvas size minus its size minus the offset, and no coordinate is `NaN`.

*Precedent: Avalonia's `Canvas.ArrangeChild`.*

### MEDIA-12: Canvas shapes are their rendered geometry

- **Given** a `Canvas` with a `Path` from path markup, a stretched `Path` with a size, a `Path` whose data is a `GeometryGroup` of a `RectangleGeometry` and an `EllipseGeometry`, a stroked `Rectangle`, an `Ellipse`, a `Line` and a `Polygon`
- **When** it is rendered
- **Then** each is the element of the shape's rendered geometry as Avalonia lays it out: a `<path>` whose `d` is the path data as written, the stretched path scaled to its size and both geometries of the group, a `<rect>` and an `<ellipse>` inset by half their stroke within their size (what Skia draws for them, without the curves of their path data), and the polygon a `<polygon>` of its points, with the fill rule.

### MEDIA-13: Strokes keep avalonias pen

- **Given** stroked shapes in a `Canvas` and outside one, with the default join, a square cap with a dash array and offset, a round cap and join, a miter limit of 4 and a bevel join
- **When** they are rendered
- **Then** each stroke carries Avalonia's pen: `stroke-linecap` and `stroke-linejoin` only from `StrokeLineCap` and `StrokeJoin` (no round caps forced and no `vector-effect` in a canvas), the dashes and the offset times the thickness, and `stroke-miterlimit="10"` for a miter join (Avalonia's default `StrokeMiterLimit`, where SVG's is 4), omitted when the limit is 4.

### MEDIA-14: Linear gradient brushes on shapes are SVG gradients

- **Given** shapes whose fill or stroke is a `LinearGradientBrush` (relative points with an opacity and a translucent stop, absolute points with `SpreadMethod="Reflect"`, `Repeat`), in a `Canvas`, on a `Path` stretched with `Fill` and on an unstretched `Path`
- **When** they are rendered
- **Then** each paint is `url(#id)` of a `<linearGradient>` in the `<svg>`'s `<defs>` with `gradientUnits="userSpaceOnUse"`, placed as Avalonia's Skia renderer places it: the points on the geometry's bounds for a fill and on the bounds inflated by half the thickness for a stroke, absolute points as written, `spreadMethod` from the spread, each stop's `stop-color` and its alpha times the brush opacity as `stop-opacity`; a stretched path's gradient is placed on its rendered bounds and mapped back into its view box by `gradientTransform`, so its stripes stay perpendicular to the gradient line as Avalonia draws them.

### MEDIA-15: Radial gradient brushes on shapes are SVG gradients

- **Given** shapes whose fill is a `RadialGradientBrush` (a gradient origin off the center on a wide rectangle, `SpreadMethod="Reflect"` with smaller radii, a `ScaleTransform` about a relative `TransformOrigin`)
- **When** they are rendered
- **Then** each is a `<radialGradient>` in user space with `cx`, `cy` from the center, `r` from `RadiusX` on the target width, a `gradientTransform` scaling by `RadiusY / RadiusX` about the center, `fx`, `fy` from the gradient origin (its height scaled back as Avalonia does), `spreadMethod`, and the brush transform about its origin.

### MEDIA-16: Canvases place any child in their order with their look

- **Given** a `Canvas` with a gradient background, shapes with a `ZIndex`, an `Opacity` and a `RenderTransform`, and a `TextBlock`
- **When** it is rendered
- **Then** the background is the CSS gradient, the children are written in `ZIndex` order, the translucent shape has `opacity`, the transformed one has `transform="matrix(...)"` composing its render transform about `RenderTransformOrigin` with its position, and the text is written in a `<foreignObject>` at its arranged bounds; what overflows the canvas stays visible (`overflow:visible`), as Avalonia draws it, unless the canvas clips to its bounds (`overflow:hidden`); a canvas in a control template writes its shapes but no other child, whose position the control's code sets at run time (Ursa's `Marquee` moves its content on a timer).

### MEDIA-17: Sized rectangles and ellipses with gradients are SVG

- **Given** a `Rectangle` with a size whose fill and stroke are gradients, an `Ellipse` with a size and a radial fill, and a `Rectangle` with a solid fill, outside a `Canvas`
- **When** they are rendered
- **Then** the first two are an `<svg>` at their size of their laid-out geometry (the stroke inside the size as Avalonia draws it) painted with the gradients on Avalonia's targets, while the solid one stays a CSS box.

### MEDIA-18: Viewboxed canvases of shapes are drawn like Avalonia

- **Given** a `Viewbox` with a size holding a `Canvas` 64 pixels square of a rounded `Rectangle` with a `LinearGradientBrush` fill and no `Canvas.Left` or `Canvas.Top`, a stroked `Path` with a size and path markup, and a placed `Rectangle` (an app icon as Pictor's Gallery draws it)
- **When** it is rendered
- **Then** the view box is the canvas size and the canvas inside is an `<svg>` of the three shapes, the gradient defined in its `<defs>` from corner to corner, the path with its round caps, and the rectangle at its position.

### MEDIA-19: Bound points in canvases follow the model

- **Given** a `Polyline` placed in a `Canvas` whose `Points` are bound to the model
- **When** the page is compiled and rendered for another model
- **Then** the plan is reusable and the polyline is a `<polyline>` at its position whose `points` are the other model's, with its stroke.

### MEDIA-20: Conic gradient fills are clipped CSS gradients

- **Given** an `Ellipse` placed in a `Canvas` and an unstretched `Path`, each filled with a `ConicGradientBrush` (the ellipse with a stroke)
- **When** they are rendered
- **Then** each fill is a `<foreignObject>` over the geometry's bounds holding a `div` whose background is the CSS `conic-gradient` (its angle, its center in pixels on those bounds), in a `<g>` placed as the shape and clipped by a `<clipPath>` of the geometry in the `<defs>`, since SVG has no conic paint; the ellipse's stroke is drawn over it by its own element with `fill="none"`, and a shape without a stroke has no other element.

### MEDIA-21: Icons a library registers are drawn

- **Given** icons a library registers with `IconSet.Register` (two kinds of its own in Lucide's 24-pixel stroke convention; a registered kind replaces a built-in one of the same name)
- **When** an `a:Icon` of each kind is rendered on the web and on the desktop
- **Then** the web writes each one's markup in the icon's `<svg>` and a bound kind written by the plan picks the registered markup too, the desktop draws the registered geometry, and the built-in icons stay as they were.

### MEDIA-22: Dashed rectangle strokes are dashed

- **Given** a `Rectangle` stretched over its panel with a 1 px `#e5e5e5` stroke, `StrokeDashArray="3,3"` and an 8 px radius (Aioros.Cosmo's outline empty state), a stretched one with a 2 px stroke and `StrokeDashArray="1,1"`, and a 40 by 20 `Rectangle` with a 1 px stroke and `StrokeDashArray="3,3"`
- **When** the page is rendered
- **Then** the first is a box whose border is `1px dashed #e5e5e5` with its radius, the second's border is `2px dotted`, and the sized one is an `<svg>` whose stroke has `stroke-dasharray="3 3"`, the dashes Avalonia draws (the array times the thickness), instead of a solid border.

### MEDIA-23: Stretched images fill their box

- **Given** an `Image` without a width or a height, stretched both ways in a panel over a cell, with `Stretch="UniformToFill"` (shadcn/ui's `absolute inset-0 h-full w-full object-cover`, Aioros.Cosmo's `login-04` and `signup-04` image column) and one with `Stretch="Fill"`, and one with `Stretch="UniformToFill"` in a vertical `StackPanel`
- **When** the page is rendered
- **Then** the first two fill their box as Avalonia arranges them (`width:100%;height:100%` with `object-fit:cover` and `object-fit:fill`), while the one in the stack, which Avalonia measures with no height, keeps `max-width:100%;height:auto`.

### MEDIA-24: An icon takes the size its parent gives icons

- **Given** a `Button` whose style sets `IconStyle.Size` to 12 (Aioros.Cosmo's `xs` and `icon-xs` buttons) holding an icon without its own size and one sized 20, and an icon outside it
- **When** the page is written and laid out
- **Then** the first icon is 12 px on the web (`width="12" height="12"`) and on the desktop, the sized one keeps 20 and the one outside keeps the default 16.

*Precedent: shadcn/ui's `[&_svg:not([class*='size-'])]:size-3` on its `xs` buttons; Avalonia's `TextElement.FontSize`, an attached property its text inherits unless it sets its own.*
