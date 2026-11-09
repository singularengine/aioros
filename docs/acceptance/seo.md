# SEO

Search and sharing metadata (`src/Targets/Aioros.Targets.Ssr` and the adapter's writers). Follows the Next.js conventions: `metadata` per page, `sitemap.ts`, `robots.ts`.

22 criteria. Each has an identifier and exactly one test.

## Seo

### SEO-01: Metadata is written in the head

- **Given** a page whose loader (SSR-47) or page model gives a description, a canonical path and an image
- **When** the page is rendered
- **Then** the head has the meta description, an absolute canonical link, Open Graph (`og:title`, `og:description`, `og:url`, `og:image`, `og:type`) and Twitter card tags, all encoded; without metadata, then only the title is written.

### SEO-02: Unindexable pages ask robots to skip them

- **Given** a page model that is not indexable
- **When** the page is rendered
- **Then** the head has `<meta name="robots" content="noindex">`.

### SEO-03: Sitemaps list public pages

- **Given** pages with and without route parameters and a sitemap source registered by the app
- **When** `/sitemap.xml` is requested
- **Then** it is a sitemap of absolute URLs: every page without parameters plus the source's URLs, without the not-found and error pages and without native-only pages, whose route answers the web with a stand-in (NATIVE-09).

### SEO-04: Robots point to the sitemap

- **Given** the app
- **When** `/robots.txt` is requested
- **Then** it allows crawling, disallows `/_aioros/` and points to the sitemap's absolute URL.

### SEO-08: Missing favicons answer no content

- **Given** an app without its own `favicon.ico`
- **When** the browser asks for it
- **Then** the answer is 204 with no body, so no error reaches the console; a `favicon.ico` in `wwwroot` is served instead when it exists.

### SEO-41: An icon in the web root is linked from the head

- **Given** an app whose web root (`wwwroot`) holds `favicon.svg` or `favicon.png`
- **When** a page or the native-only stand-in is rendered
- **Then** the head links it as `<link rel="icon" href="/favicon.svg" type="image/svg+xml">` (`image/png` for the PNG; the SVG wins when both exist), as Next.js links an `icon` file of the app folder; an app without either gets no icon link, and `/favicon.ico` keeps SEO-08's answers.

## Seo controls

### SEO-05: Containers take semantic elements

- **Given** a container with `a:Web.Tag` set to `nav`, `header`, `footer`, `section`, `article`, `aside` or `main`
- **When** it is rendered
- **Then** it is written with that element; and the content of a layout's outlet is written inside `<main>` unless the page already has one.

### SEO-06: Items are announced as lists

- **Given** an `ItemsControl` with items
- **When** it is rendered
- **Then** its panel has `role="list"` and each item is wrapped in `<div role="listitem" class="li">`, so assistive technology announces a list without changing the layout: the style sheet's `.li{display:contents}`, written once, lays the wrapper out of the way instead of an inline style on every item.

### SEO-07: Automation names become aria labels

- **Given** controls with `AutomationProperties.Name` (an icon-only button, a link container, a text input)
- **When** they are rendered
- **Then** each element carries it as `aria-label`; and icons are written with `aria-hidden="true"` because they decorate their label.

### SEO-31: Converted automation names are planned

- **Given** a control whose `AutomationProperties.Name` is bound through a converter (as Avalonia's color palette names each swatch through `ColorToDisplayNameConverter` in Semi's color picker)
- **When** the page is compiled and rendered for other models
- **Then** the plan is reusable and each render, also after the plans file, writes the converted text as its `aria-label`, as Avalonia's render does.

### SEO-42: Heading levels become headings

- **Given** `TextBlock`s whose `AutomationProperties.HeadingLevel` is 1 to 6 (set by a style, as Aioros.Cosmo's typography classes set it), one whose level is 8, a `Border` with level 3 and a `TextBlock` with the `h2` class and no level
- **When** the page is rendered
- **Then** each text with level 1 to 6 is written in that element (`<h3>`, `<h4>`), the level 8 text in a `<div role="heading" aria-level="8">`, the border in a `<div role="heading" aria-level="3">` box, and the `h2` class keeps its `<h2>` (TEXT-01); the page's style sheet resets, once, the margin, size and weight of `h3` to `h6` (`h3,h4,h5,h6{margin:0;font-size:inherit;font-weight:inherit}`, as the base styles reset `h1` and `h2`), so a heading keeps the text's own look; a level of 0 writes a `<div>`.

*Precedent: HTML-AAM and WAI-ARIA map `h1`-`h6` and `role="heading"` with `aria-level` to the heading role and level that Avalonia's peers report through `AutomationProperties.HeadingLevel` (UIA's `HeadingLevel`).*

### SEO-43: Live settings become live regions

- **Given** a templated control whose `AutomationProperties.LiveSetting` is `Assertive` (as Aioros.Cosmo's `c:Alert`), a templated control with `Polite` and only one template part (as `c:Spinner`, which would otherwise be written as its part alone) and a `TextBlock` with `Polite`
- **When** the page is rendered
- **Then** the assertive control's box has `role="alert"`, the polite control keeps its own box with `role="status"` and its `aria-label`, the text has `role="status"`, and a control with `Off` has no role.

*Precedent: WAI-ARIA's `alert` role is an assertive live region and `status` a polite one, the counterparts of UIA's `LiveSetting` that Avalonia's peers report.*

### SEO-44: Landmark types become landmarks

- **Given** containers whose `AutomationProperties.LandmarkType` is `Navigation`, `Main`, `Banner`, `ContentInfo`, `Complementary`, `Region`, `Search` and `Form` (set by a control theme or a constructor, as Aioros.Cosmo's `c:Pagination`, `c:Breadcrumb` and `c:NavigationMenu`), and a `Navigation` one with `a:Web.Tag="aside"`
- **When** the page is rendered
- **Then** each is written as `<nav>`, `<main>`, `<header>`, `<footer>`, `<aside>`, `<section>`, `<search>` and `<div role="form">`, with its `AutomationProperties.Name` as `aria-label`, also an items control written as its panel and a wrapper of one child; the tagged one is an `<aside>`, since `a:Web.Tag` wins.

*Precedent: HTML-AAM maps these elements to the ARIA landmark roles that UIA's landmark types (`AutomationLandmarkType`) name.*

### SEO-45: Control types become roles

- **Given** containers whose `AutomationProperties.ControlTypeOverride` is `List`, `ListItem`, `Group` and `Separator`, and one whose `AccessibilityView` is `Raw`
- **When** the page is rendered
- **Then** they have `role="list"`, `role="listitem"`, `role="group"`, `role="separator"` and `aria-hidden="true"`; a control type without an ARIA counterpart (`Text`, `Custom`) writes no role.

*Precedent: WAI-ARIA's `list`, `listitem`, `group` and `separator` roles are the counterparts of UIA's control types; a raw view leaves the element out of the accessibility tree as `aria-hidden` does.*

### SEO-46: Library themes name their elements

- **Given** a library that references Avalonia and Aioros.Markup only, whose control theme sets `a:Web.Tag="kbd"` on its `ContentControl` (as Aioros.Cosmo's `c:Kbd`), and a page with a `StackPanel` tagged `ol` holding `Border`s tagged `li`, a `TextBlock` tagged `code` and a `TextBlock` tagged `p`
- **When** the page is rendered
- **Then** the key is a `<kbd>` holding its content, the list an `<ol>` of `<li>` items, the texts a `<code>` and a `<p>`, and the style sheet resets once, with no specificity (`:where(...)`), the margins, padding, list markers and monospace font browsers give these elements, so each keeps Avalonia's look; a `p` on a container still writes a `<div>`, since a `<p>` cannot hold the blocks a container writes.

*Precedent: react-native-web, which picks an element from a view's role and `href` and ignores it on native; Avalonia, whose XAML namespace URI spans every assembly that declares it with `XmlnsDefinition`.*

### SEO-47: A field set is written with its legend

- **Given** a library's `StackPanel` tagged `fieldset` (as Aioros.Cosmo's `c:FieldSet`) whose first child is a `TextBlock` tagged `legend`, followed by a text and a `TextBox`
- **When** the page is rendered
- **Then** the panel is a `<fieldset>` whose first child is a `<legend>` holding the text, and the style sheet resets once, with no specificity, the border, margins, padding and minimum width browsers give a fieldset and the padding they give a legend; and a `legend` on a text that is not the first child of a fieldset (a second child, or the child of an untagged panel) writes a `<div>`, as a `fieldset` on a text does, since HTML allows a legend only as a fieldset's first child and a fieldset only as a box.

*Precedent: HTML's content model of `fieldset` ("optionally a `legend` element, followed by flow content"), and Aioros's own `GroupBox`, written as a `<fieldset>` with a `<legend>` (LAYOUT-15).*

### SEO-48: A legend takes part in its field sets layout

- **Given** a `StackPanel` tagged `fieldset` with a `Spacing` of 24 whose first child is a `TextBlock` tagged `legend` with a bottom margin of -12, followed by two texts
- **When** Chromium lays the page out at 400 and 800 px
- **Then** every text stands where Avalonia places it on the desktop: the legend takes part in the panel's layout as the `<div>` it replaces would (the reset floats it, so it is not HTML's rendered legend drawn over the border and outside the flex gap, and as a flex item its float is ignored).

*Precedent: HTML's rendering section on `fieldset` and `legend` (a floated legend is not the rendered legend and lays out in the fieldset's anonymous content box), CSS Flexbox (`float` does not apply to a flex item).*

### SEO-49: Current marks are written as aria current

- **Given** a `Border` with `a:Web.Current="page"`, a `TextBlock` with `a:Web.Current="step"`, a `Button` with `a:Web.Current="true"`, a `TextBlock` with `a:Web.Current="bogus"`, and an item template whose `TextBlock` binds `a:Web.Current` to its item's mark (`page` for one item, null for the others)
- **When** the page is rendered for two models whose current item differs
- **Then** each element carries `aria-current` with its value (`page`, `step`, `true`), the bogus value writes nothing, and only the bound item whose mark is set carries `aria-current="page"`, the plan and the plans file writing what Avalonia renders for each model.

*Precedent: WAI-ARIA's `aria-current` (`page`, `step`, `location`, `date`, `time`, `true`), which shadcn/ui's breadcrumb page and pagination link carry.*

## Loader result

### SEO-09: Layout and page metadata merge

- **Given** a layout loader and a page loader that both return a title and metadata
- **When** the page is rendered
- **Then** the deepest title wins and the layout's title is used when the page gives none; the page's description, canonical path and image win where it sets them and the layout's fill the rest; the type is the page's; and the page is not indexable when any of them says so, so a section marked `noindex` stays out of search.

*Precedent: Next.js resolves segment metadata from the root layout to the page and merges it shallowly, the deepest segment winning.*

### SEO-50: Alternates in other languages are written in the head

- **Given** a page whose loader gives alternates in other languages (`es` at `/es`, `zh-Hans` at `/zh`, `x-default` at `/` and one absolute URL)
- **When** the page is rendered
- **Then** the head has one `<link rel="alternate" hreflang="…" href="…">` per alternate, in their order, each path made absolute on the request's origin and the URL kept; a page without alternates has none; and a page that gives none under a layout that gives them keeps the layout's, as its description is kept (SEO-09).

*Precedent: Google's "Tell Google about localized versions of your page" (`hreflang` links with `x-default`), Next.js `metadata.alternates.languages`.*

## Page head

### SEO-10: Static titles and metadata merge with loaders

- **Given** a layout and a page whose root elements declare a static title and metadata in the XAML (`a:Page.Title`, `a:Page.Description`, `a:Page.Canonical`, `a:Page.Image`, `a:Page.Type`, `a:Page.Indexable`) and have no view model
- **When** the page is rendered
- **Then** the head has the page's title and the metadata merged from the layout to the page as SEO-09
- **When** a loader in the same segment that returns a title or metadata
- **Then** the loader's title replaces that segment's static title and the loader's metadata wins field by field over the segment's static metadata, which fills what the loader leaves out.

*Precedent: Next.js static `export const metadata` per segment, merged from the root layout to the page.*

### SEO-11: Layout title templates apply to the titles below

- **Given** a layout with `a:Page.Title="Lumen"` and `a:Page.TitleTemplate="%s · Lumen"`
- **When** a page below it with a title (static or from its loader) is rendered
- **Then** the title is the template with `%s` replaced by the page's title; the layout's own title is not templated, since a template applies only to the segments below it; a deeper layout's template replaces an outer one; a page without a title gets the deepest title above it unchanged; and a page with `a:Page.AbsoluteTitle` gets that title as written, ignoring every template.

*Precedent: Next.js `title.template`, `title.default` and `title.absolute` (nextjs.org/docs/app/api-reference/functions/generate-metadata#template).*
