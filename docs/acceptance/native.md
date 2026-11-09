# Platforms in the XAML and native-only content

What a page shows on the web and in the installed app when the two differ: `a:OnPlatform` with its `Web` value, Avalonia's `OnFormFactor` on the web, and pages declared native-only with `a:Page.Platforms`, which the web answers with a stand-in (`src/Adapters/Aioros.Avalonia`, `src/Aioros.Generators`, `src/Aioros.Routing`, `src/Targets`). See "Platforms" in [docs/architecture.md](../architecture.md#decisions).

Precedents: Avalonia's `OnPlatform` and `OnFormFactor`, which its XAML compiler turns into one branch per option property marked `[MarkupExtensionOption]`, tested by the extension's `ShouldProvideOption` when the XAML is loaded; Uno Platform's `xmlns:wasm` for a web branch in XAML; Expo Router, which allows a platform-only route only beside a universal fallback so a deep link works everywhere.

14 criteria. Each has an identifier and exactly one test.

## Platform value

### NATIVE-01: The web value is written on the web

- **Given** a control whose value is `{a:OnPlatform Default=…, Windows=…, macOS=…, Linux=…, Android=…, iOS=…, Web=…}`
- **When** the page is rendered for the web
- **Then** the `Web` value is written whatever the server's operating system, and the page plans: the plan reproduces Avalonia's render byte for byte and is reused.

### NATIVE-02: Without a web value the default is written

- **Given** an `a:OnPlatform` with a value for the server's operating system and a `Default` but no `Web` value
- **When** the page is rendered for the web
- **Then** the `Default` value is written, never the server's operating system's; with neither `Web` nor `Default`, then the value is empty (the type's default, as Avalonia's own `OnPlatform` gives when no branch applies).

### NATIVE-04: Sections and variants follow the platform

- **Given** a section with `IsVisible="{a:OnPlatform Default=True, Web=False}"`, a section with the opposite, and a content variant (`Content` and `Text` chosen with `a:OnPlatform`)
- **When** the page is rendered for the web
- **Then** the native-only section is not written, the web-only section is, the variant's web content is written, and the page plans.

### NATIVE-05: Form factors are desktop on the web

- **Given** Avalonia's own `{OnFormFactor Default=…, Desktop=…, Mobile=…}`
- **When** the page is rendered for the web
- **Then** the `Desktop` value is written: the server writes one document for every viewport, and what changes with the viewport is said with responsive CSS (`a:Web.HideBelow`, `a:Web.StackBelow`).

## Desktop platform

### NATIVE-03: The running systems value is shown on the desktop

- **Given** a page of the desktop app with `{a:OnPlatform Default=…, Windows=…, macOS=…, Linux=…, Web=…}` and `{a:OnPlatform Default=…, Web=…}`
- **When** the desktop navigator shows it
- **Then** the first shows the running operating system's value and the second its `Default`; the `Web` value is never chosen on the desktop.

### NATIVE-12: Native only pages show the real page on the desktop

- **Given** a native-only page
- **When** the desktop navigator goes to its route
- **Then** the real page is shown, never the stand-in.

## Page platform generator

### NATIVE-06: Avalonias on platform is reported where the web renders it

- **Given** an `.axaml` file that uses Avalonia's own `OnPlatform` (as a markup extension or an element)
- **When** the app is built
- **Then** `AIOROS030` warns at that file that the web would render the server's operating system's value and names `a:OnPlatform`, which has the same options and a `Web` value; a file using `a:OnPlatform`, and a page that is native-only, get no warning.

### NATIVE-07: Platforms are read from the page root

- **Given** pages whose root elements declare `a:Page.Platforms="Desktop"`, `a:Page.Platforms="Desktop, Mobile"` and nothing
- **When** the app is built
- **Then** their page definitions carry `Desktop`, `Desktop` and `Mobile`, and every platform (`All`)
- **When** an unknown platform name
- **Then** `AIOROS031` is an error at that file naming the known ones (`Web`, `Desktop`, `Mobile`).

### NATIVE-08: The apps stand in is the native only page

- **Given** `Pages/NativeOnly.axaml` and layouts in `Pages/`
- **When** the files are mapped
- **Then** it is not a route but the native-only stand-in, with the layouts of its folder, and the generated registry and app carry it as `NativeOnly`; without the file, `NativeOnly` is null.

## Native only

### NATIVE-09: Native only routes answer with the built in stand in

- **Given** a page whose platforms leave out the web and an app without its own stand-in
- **When** its route is requested on the web
- **Then** the answer is `200` with the built-in stand-in: the page's title (with its layouts' title template) and description, "Available in the desktop app", an "Open in the app" link to the configured download address marked `data-open-app` (the mark of `a:Web.OpenInApp`, which tries the app's scheme at the page's path, INSTALL-09) and a "Download" link to the same address, `noindex`, and `Cache-Control: no-store`; the page's loaders do not run and the engine is not asked; a post to the route is not served.

### NATIVE-10: The apps stand in is rendered with its own models

- **Given** the same page and an app with `Pages/NativeOnly.axaml`
- **When** its route is requested on the web
- **Then** the answer is `200` with the app's stand-in rendered with its own models and layouts under the native-only page's title, and `Cache-Control: no-store`, so it is never kept as that route's content.

### NATIVE-11: Installed apps get the real pages data

- **Given** a native-only page with a loader
- **When** the installed app asks `/_data` for its route
- **Then** the answer is the real page's loader data, not the stand-in.

### NATIVE-13: Native only pages are not precompiled

- **Given** an app with a native-only page and its own stand-in
- **When** the pages to precompile and warm are listed
- **Then** the native-only page is left out (the web never renders it) and the stand-in is in, beside the not-found and error pages.

### NATIVE-14: The stand in loads its open in app script from a file

- **Given** the native-only stand-in of an app with an installed app's scheme (`SsrOptions.AppScheme`)
- **When** it is answered
- **Then** its "open in the app" script is a file it loads (`<script src="/_aioros/open/{hash}.js" defer>`, immutable), never inline, so a Content Security Policy without `'unsafe-inline'` for scripts holds on it as on every other page (the security audit's F5).
