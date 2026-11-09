# Charts

`a:Chart` with line, area, column, candlestick and donut series (`src/Adapters/Aioros.Avalonia/Charts`). OxyPlot.Core lays the chart out; on the web it is written as inline SVG by a plan slot, without Avalonia, and on the desktop it draws through Avalonia's `DrawingContext`. Both measure text with the same Inter metrics, so the layout is the same.

11 criteria. Each has an identifier and exactly one test.

### CHARTS-01: Bound series follow the model

- **Given** a chart with an area series and a line series whose items are bound to view-model lists
- **When** the page is compiled
- **Then** the plan is reusable and renders an inline SVG with one path per series that follows the bound values, and rendered for another model it follows that model's values.

### CHARTS-02: Charts scale and keep IDs apart

- **Given** two charts on one page
- **When** it is rendered
- **Then** each chart fills its element's width at its own height: lines, areas and bars stretch over the plot area through a `viewBox` without thickening their strokes, axis text is HTML that keeps its size, vertical-axis labels keep their distance from the edge and horizontal-axis labels follow the plot area through `calc`, so the chart reads at a phone's width; a donut keeps its proportions; its clip ids are unique on the page, and the chart's element is an image labelled with its title for assistive technology.

### CHARTS-03: SVG is reused for the same data

- **Given** a chart rendered for the same list instance twice with the values it reads unchanged
- **When** the second render runs
- **Then** it reuses the first SVG
- **When** a new list instance
- **Then** the SVG is made again.

### CHARTS-04: Columns candles and donuts are drawn

- **Given** column, candlestick and donut series
- **When** they are rendered
- **Then** a column series draws one bar per item with its height, a candlestick series a body and a wick per item with rising and falling colors, and a donut series one slice per item with its share of the total.

### CHARTS-05: Charts draw natively

- **Given** a chart on the desktop
- **When** it is drawn
- **Then** it draws through Avalonia's `DrawingContext`: a captured frame shows the series color along the series and the background elsewhere.

### CHARTS-06: Theme colors follow the theme

- **Given** a series whose stroke is a theme resource
- **When** the page is compiled in the light and the dark theme
- **Then** each plan draws the series with that theme's color.

### CHARTS-07: Chart plans serialize

- **Given** a page with a bound chart
- **When** its plan is serialized and read back through the adapter's resolver
- **Then** it renders the same SVG.

### CHARTS-08: Text is measured alike on both targets

- **Given** text in a chart
- **When** it is measured on the web and on the desktop
- **Then** both use the same Inter metrics, so an axis label has the same width in both.

### CHARTS-09: Chart errors fail the render

- **Given** series that OxyPlot cannot lay out together (a column series on a date axis)
- **When** the page is rendered
- **Then** rendering fails with an error that names the chart, and OxyPlot's error text and stack trace are never written into the page.

### CHARTS-10: Axes fit the data

- **Given** an area series of prices far from zero, a date series within one day, and a donut without a width
- **When** they are rendered
- **Then** the area fills down to its lowest value so the vertical axis fits the data, the date axis labels hours (`HH:mm`), and the donut is laid out in a square of its height.

### CHARTS-11: SVG follows a list changed in place

- **Given** a chart rendered from a plain `List<T>` of items without change notification, through its plan's slot and through the engine's writer
- **When** the same list instance is rendered again after an item is added to it, and again after an item's plotted value is changed in place
- **Then** each render makes the SVG again and draws the new values (one more point, the moved point), and a render with nothing changed reuses the last SVG.

*Precedent: React's `useMemo`, which recomputes when a dependency differs by value comparison rather than by the object that holds it, and Recharts, which redraws when its `data` changes.*
