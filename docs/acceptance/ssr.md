# Server rendering

The SSR target (`src/Targets/Aioros.Targets.Ssr`): it turns a request into the page's models, asks the page engine for a plan once, and writes every later response from the cached plan. Tests run the handlers on an in-memory `HttpContext` with a fake engine.

273 criteria. Each has an identifier and exactly one test.

## Page handler

### SSR-01: Pages are written as complete documents

- **Given** a page
- **When** it is requested
- **Then** the response is `text/html; charset=utf-8` with the doctype, the page's title (from its loaders, SSR-47, or its page model as a fallback), the page's style sheet, the document colors, the body from the plan inside a form marked `novalidate` (the server validates: a browser bubble on an `email` input would otherwise block the post and the model's own errors; Django admin's forms and Conform's enhanced forms, Cosmo's need 22), and the navigation script's element (SSR-190).

### SSR-02: Reusable plans are compiled once

- **Given** a reusable compiled page
- **When** it is requested twice
- **Then** the engine compiles it once and the second response comes from the cached plan with the new model's values.

### SSR-03: Pages that are not reusable render every time

- **Given** a compiled page that is not reusable
- **When** it is requested twice
- **Then** the engine renders it both times, trying to compile its plan only the first time: the second request is rendered only to answer it, with the first failure as its reason, until the plans are cleared (a XAML reload), as a JIT stops recompiling a method whose optimized code keeps failing (V8's and HotSpot's deoptimization limits).

### SSR-04: Missing models answer not found

- **Given** a page model that reports not found after loading
- **When** the page is requested
- **Then** the status is 404.

### SSR-05: Route values and queries reach the loader

- **Given** route values and a query string
- **When** the page is requested
- **Then** the page model's `LoadAsync` receives both.

### SSR-06: Themes have their own plans

- **Given** the theme cookie set to `dark`
- **When** the page is requested
- **Then** the engine compiles it for the dark theme and that plan is cached apart from the light one.

### SSR-07: The theme switch toggles and goes back

- **Given** `/theme/toggle` requested from a page in the light theme
- **When** it is handled
- **Then** the theme cookie becomes `dark` and the response redirects to the referring page's path, or to `/` when the referrer is another site.

### SSR-08: Posts redirect conflict or render

- **Given** a form post
- **When** the engine applies it
- **Then** the fields reach the engine; the response redirects (303) when the page model sets `RedirectTo`, is 409 on a conflict, 400 on a bad request, and otherwise the rendered page.

### SSR-09: Reloads drop plans and notify the browser

- **Given** a cached plan
- **When** a XAML source of the page is reloaded
- **Then** the cache drops the plan and the development channel sends a `reload` event
- **When** a reload fails
- **Then** it sends an `error` event with the message.

### SSR-10: Live pages stream their changes

- **Given** a live page
- **When** its model raises `PropertyChanged`
- **Then** the live stream sends the body re-rendered with the new values as a server-sent event.

### SSR-11: Layout models come first

- **Given** a page with layouts
- **When** it is requested
- **Then** every layout's model and the page's model are created and loaded, and they reach the engine in layout order with the page's model last.

### SSR-12: Loaders redirect without rendering

- **Given** a page model that sets `RedirectTo` while loading
- **When** the page is requested
- **Then** the response redirects (302) there without rendering, as `redirect` does in Next.js.

### SSR-13: Changes in one UI job are sent once

- **Given** a live page whose model changes several times in one job of the UI thread
- **When** the stream renders
- **Then** it waits for that job to finish and sends one event with the final values.

### SSR-14: Pages are captured by path

- **Given** development mode
- **When** `/_aioros/capture?path=/issues/7&theme=dark&width=800&height=600` is requested
- **Then** the response is the engine's PNG of the page matching that path, with its route values loaded into its models, the theme and the size; a path that matches no page answers 404.

### SSR-15: Forms carry antiforgery tokens when turned on

- **Given** an app that turns ASP.NET Core antiforgery tokens on (`AddAntiforgeryTokens`, off by default, SSR-99)
- **When** a page with form fields is rendered
- **Then** its form carries the request verification token, and a page without fields carries none
- **When** a post arrives without a valid token
- **Then** it is rejected with 400 before the engine sees it.

### SSR-16: Not found pages are rendered with 404

- **Given** a not-found page
- **When** a path matches no route or a page model reports not found
- **Then** the not-found page is rendered with its layouts and status 404; without one, then the response is an empty 404.

### SSR-17: Error pages are rendered with 500

- **Given** an error page
- **When** it is rendered for a failed request
- **Then** it has status 500 and shows the page, never the exception; without one, then the response is an empty 500.

### SSR-18: Inter is served as avalonias static woff 2

- **Given** the web fonts
- **When** their style sheet is written
- **Then** Inter is declared as the static woff2 files of the Inter Avalonia embeds (`Avalonia.Fonts.Inter`, Inter 3.019, whose advances and vertical metrics differ from Inter 4's), one per weight Avalonia ships (Thin 100, Light 300, Regular 400, Medium 500, SemiBold 600, Bold 700) for the Latin and Latin Extended ranges with `font-display:swap`, so the browser lays text out with Avalonia's glyph widths
- **When** a font file is requested by name
- **Then** its bytes are returned with a long cache, and an unknown name returns nothing.

### SSR-19: Static pages are compiled at startup

- **Given** pages without route parameters
- **When** the app warms up
- **Then**
  - Each is compiled for the light and the dark theme and its reusable plans are cached, so the first visit is served from a plan; a page with parameters is compiled with the values of the first URL that matches it from the app's precompile sources or its sitemap, and is left for its first visit when there is none; a page that fails to load does not stop the others.
  - Every route is reported with its cached entries or the reason it has none (route parameters without a sample URL, the location its model redirects to or that it is not found, a plan that is not reusable with the compiler's reason, an error).

### SSR-20: Live streams are compressed per connection

- **Given** a client that accepts gzip
- **When** a live page streams
- **Then** the stream is gzip-compressed with one window for the whole connection and every event is flushed whole, so each update arrives at once and nearly identical bodies cost a fraction of their size; a connection cancelled in the middle of a flush still ends with a valid stream, with no event sent twice.

### SSR-21: Reusable pages link hashed stylesheets

- **Given** external stylesheets enabled (production)
- **When** a reusable page is written
- **Then** its base and page CSS are `<link>`s to `/_aioros/css/{content hash}.css`, served as `text/css` with `Cache-Control: public, max-age=31536000, immutable` and the same text, and an unknown name is 404; a page that is not reusable keeps its CSS inline.

### SSR-22: Navigation requests get only the page

- **Given** a request from the navigation script (`X-Aioros-Navigation: 1`)
- **When** a page is written
- **Then** the response holds only what navigation uses: the title, the live marker, the plan's fingerprint, the stylesheets, the body style and the form with the page, without scripts or search metadata, and it varies on that header and on `X-Aioros-Layout` (and on `Cookie`, as every answer that may depend on the visitor, SSR-231); a normal request gets the complete document.

### SSR-23: Layouts can redirect

- **Given** a layout model that sets `RedirectTo` (when it loads, or in a command it runs)
- **When** its page is requested or posted to
- **Then** the answer is a redirect to that location (`302` for a request, `303` after a post); when the page model also sets one, the page's wins.

*Precedent: Next.js `redirect` in a layout.*

### SSR-24: Live updates carry the CSS of plans that are not reusable

- **Given** a live page whose plan is not reusable (for example a list that was empty when it was compiled)
- **When** it streams an update rendered with CSS the document did not have
- **Then** that event and every later one start with the page CSS in a `<style data-live-css>` element inside the body, so the new markup is styled; a reusable page whose CSS the document already has streams only its body.

### SSR-26: Pages the browser knows answer with their dynamics

- **Given** a reusable page
- **When** it is written
- **Then** its head names its plan's fingerprint in `<meta name="aioros-plan">` (a page that is not reusable names none), and `/_aioros/plan/{fingerprint}` answers the plan's template as JSON that browsers may cache for good (`404` for an unknown fingerprint), written from the plan when asked for, since a plan keeps only its template's fingerprint and length
- **When** a request (a navigation, a post rendered in place, or the page after a post's redirect) carries `X-Aioros-Plan` with that fingerprint
- **Then** the answer is JSON with the title (`t`) and the body's dynamics (`d`) instead of HTML; with another fingerprint, the HTML page.

### SSR-29: Pages are rendered for their path

- **Given** a page with links
- **When** it is written as a document, as dynamics or as a live update
- **Then** it is rendered for the request's path (for a live stream, the page's path, not `/live`), so the link to the current page is marked.

### SSR-30: Navigations that keep the layout get the page alone

- **Given** a page inside layouts
- **When** it is written
- **Then** its head carries `<meta name="aioros-layout">` with a signature of everything outside the page (statics and values, not the marks of the current link), the same for two pages whose layouts render the same, wherever their current link's mark falls
- **When** a navigation request sends that signature in `X-Aioros-Layout`
- **Then** the answer is marked `X-Aioros-Partial: 1` and its body holds only the page, without the layouts; with another signature, the whole body.

*Precedent: Next.js keeps layouts across navigations; React Router reruns parent loaders, so a layout that changes is sent again.*

### SSR-107: Captures answer JSON in a state

- **Given** development mode
- **When** `/_aioros/capture?path=/issues/7&state=pointerover&format=json` is requested
- **Then** the response is JSON with the engine's PNG in base64 (`png`) and its laid-out texts (`texts`: `text`, `x`, `y`, `width`, `height`), captured in that state; without `state` the state is `normal`, and an unknown state answers 400.

### SSR-166: Post path is warmed without binding or running anything

- **Given** pages warmed at startup (SSR-19)
- **When** the server warms up
- **Then**
  - The post handling runs once, on the page (not a special page such as not-found) with the smallest plan whose fields that plan binds without the engine, with a post whose button names no field of the page (`_btn=2147483647|`): it answers `409`, binds no field, runs no command and never asks the engine to post, and the warm-up reports that status for that page and none for the others; pages warmed for `--precompile` are not posted.
  - The post path is the framework's and the same for every page, so one post compiles it for the first real post after a start (ASP.NET Core's warm-up practice: exercise the request path before traffic arrives, here in process and without side effects), and the smallest plan renders the least of the app's data (a page whose models hold Avalonia types would load Avalonia at start).

### SSR-170: The system theme follows the color scheme hint

- **Given** `/theme/system` requested
- **When** it is handled
- **Then** the theme cookie becomes `system` and the redirect asks for the `Sec-CH-Prefers-Color-Scheme` client hint (`Accept-CH`)
- **When** a visitor whose cookie is `system`, when a page is requested with the hint `"dark"`
- **Then**
  - It is rendered with the dark plan, and with the hint `"light"` or without the hint (a browser that sends none) with the light plan, the plan per theme staying light or dark; the page's answer to that visitor carries `Accept-CH` and `Critical-CH` naming the hint (so a first request without it is retried with it) and `Vary` including it, while an answer to a visitor who chose a theme carries neither
  - `/theme/toggle` from `system` resolved dark sets `light`.

*Precedent: Chromium's user-preference media-feature client hints (`Sec-CH-Prefers-Color-Scheme`, with `Critical-CH` for the first navigation), as web.dev's "User preference media features client hints headers" describes.*

### SSR-171: The theme cookie is secure only over HTTPS

- **Given** `/theme/dark` requested over plain HTTP (a LAN address, where a browser drops a `Secure` cookie)
- **When** it is handled
- **Then** the theme cookie is not marked `secure`, and the next request carrying it is rendered with the dark plan; over HTTPS the cookie is marked `secure`.

*Precedent: ASP.NET Core's `CookieSecurePolicy.SameAsRequest`, the default of its antiforgery and session cookies.*

### SSR-176: The system theme falls back to the scripts cookie

- **Given** a visitor whose theme cookie is `system` and whose `theme-system` cookie is `dark`
- **When** a page is requested without the `Sec-CH-Prefers-Color-Scheme` hint (Firefox and Safari send none)
- **Then**
  - It is rendered with the dark plan; with the hint `"light"` as well, the hint wins and the light plan renders; with neither the hint nor the cookie, the light plan; a visitor who chose `light` with `theme-system=dark` gets the light plan.
  - The theme cookie is written without `HttpOnly`, so the framework's script can tell a visitor who follows the system.

*Precedent: next-themes and Remix's theme examples, which keep the theme where the page's script reads it and resolve `system` from `prefers-color-scheme` in the browser.*

### SSR-183: The theme switch goes back only to a local path

- **Given** `/theme/dark` requested with a same-host `Referer` whose path starts with `//` (`http://app.test//evil.example/x`) or with `/\` (`http://app.test/\evil.example/x`, which `Uri` normalizes to `//`)
- **When** it is handled
- **Then** the switch redirects to `/`, never to a protocol-relative location that leaves the site; a same-host `Referer` with a local path (`/issues?x=1`) still redirects back to it.

*Precedent: ASP.NET Core's `IUrlHelper.IsLocalUrl` and `LocalRedirect`, which refuse `//` and `/\`; the installed app's return path, checked the same way (`InstalledAppReturnPath`).*

### SSR-187: Button posts go through the engine while it runs Avalonia

- **Given** a page whose plan binds and runs its posts
- **When** a post presses its command button while the engine runs Avalonia in the process (`IPageEngine.OwnsCommands`: the engine has started)
- **Then** the post is run by the engine, on Avalonia's thread, never by the plan off it: a command that opens an overlay message box (UrsaDemo's `/messagebox`) shows it as a prompt instead of failing with Avalonia's thread check; a post that only changes a field still goes through the plan, and while no engine runs (a precompiled app that never started Avalonia) the plan runs the command; the warm-up then posts nothing through the plan.

*Precedent: WPF's and Avalonia's dispatcher, where what a command creates or touches belongs to the UI thread (`Dispatcher.VerifyAccess`).*

### SSR-190: The frameworks script is an immutable file

- **Given** a page
- **When** its document is requested
- **Then** its head references the framework's script as `<script src="/_aioros/app/{hash}.js" defer>` (the hash naming its content) and the document carries no inline copy of it
- **When** that path is requested
- **Then** the script is answered as `text/javascript` with `Cache-Control: public, max-age=31536000, immutable`, and a path with another hash answers `404`, so a browser downloads it once per version and every later document is smaller by the script.

*Precedent: Next.js, Blazor and Turbo, which serve their runtimes as hashed, cacheable files.*

### SSR-233: Refused posts still answer the page

- **Given** a form post the engine refuses, as a conflict (a button the page no longer shows, a control's state that no longer applies) or as a bad request
- **When** it is answered
- **Then** the answer keeps its status (409 or 400) and carries the page rendered afresh for the page's models, so the browser shows the page rather than its own error page.

### SSR-244: The warmup reports when it finishes

- **Given** the startup warm-up's outcomes (a page compiled, a page whose loader throws)
- **When** the warm-up is reported
- **Then** each page that failed is logged as a warning naming its route, and one information entry `WarmupFinished` says the warm-up finished with how many of the pages compiled and how long it took, the signal a host, a startup probe or a test waits for before sending traffic, instead of racing the warm-up's own compilations.

*Precedent: Kubernetes startup probes and ASP.NET Core's readiness health checks, which gate traffic on the app's own signal; IIS Application Initialization, which ends its warm-up with a logged event.*

### SSR-272: Another site cannot switch the theme

- **Given** `GET /theme/{name}` asked by another site (`Sec-Fetch-Site` `cross-site` or `same-site`: an image or a link on another page)
- **When** it is answered
- **Then** it sets no cookie and redirects to `/`, while one from the app's own page (`same-origin`), typed by the visitor (`none`) or from a browser that sends no fetch metadata switches the theme as before: a safe method should not change state at another site's request (the security audit's F7; precedent: Go's `CrossOriginProtection`, applied by "Cross-site requests" to unsafe methods).

### SSR-281: The warmup names the pages it could not plan

- **Given** the startup warm-up's outcomes with a page it compiled but could not keep a plan for (not reusable, with its reason), a page skipped for redirecting and a page whose route has no sample URL
- **When** the warm-up is reported
- **Then**
  - One warning `WarmupPageUnplanned` names the page that could not be planned and its reason (`/dashboard is answered by the engine: …`), led by the place in its XAML that kept the plan when it is known (`/app/Pages/dashboard/Page.axaml(12,6): /dashboard is answered by the engine: …`, ENGINE-306), the skipped and the unsampled pages log none, and `WarmupFinished` still counts the compiled pages.
  - A page the warm-up cannot plan loads Avalonia on every request, which the count alone (Cosmo's CI bench: `349 of 350`) does not say.

*Precedent: Next.js's build output listing each route as static or dynamic and why it opted out of prerendering.*

### SSR-291: An app can follow the device theme by default

- **Given** an app whose default theme is `system` (`Aioros:Theme`, `SsrOptions.Theme`) and a visitor with no theme cookie
- **When** a page is requested with the color-scheme hint `"dark"`
- **Then** it is rendered with the dark plan, and without the hint with the light plan; the answer asks for the hint (`Accept-CH`, `Critical-CH`, `Vary`, as SSR-170) and keeps `system` in the visitor's theme cookie, so the framework's script follows the device from then; a visitor who chose `light` keeps it and gets no hint headers; an app with no default renders a visitor without a cookie light with no hint headers and no cookie, as before.

*Precedent: next-themes' `defaultTheme="system"`, the common default of a site that follows the device until the visitor chooses.*

## Form submit

### SSR-25: Forms are posted without reloading the page

- **Given** a page whose form is submitted (a button with a command)
- **When** the navigation script handles it
- **Then** it posts the form's fields and the button with `fetch` and the page does not reload: an answer rendered in place is morphed into the form, keeping the URL and the scroll position; an answer after a redirect (`303`) shows the new page and pushes its URL; an answer that is not an HTML page (a `400`) falls back to the browser's own submit.

*Precedent: Turbo Drive form submissions and Turbo 8 morphing.*

### SSR-27: Commands answered in place arrive as dynamics

- **Given** a page whose plan template the navigation script has loaded
- **When** a command is answered in place
- **Then** the script asks with `X-Aioros-Plan`, receives the dynamics as JSON instead of HTML, joins them with the template and morphs the result into the page, which then equals the server's HTML for the same state (values, conditions and lists), without a reload.

### SSR-28: Live updates arrive as dynamics

- **Given** a live page whose plan template the navigation script has loaded
- **When** its model changes
- **Then** the script's stream asks with the fingerprint (`/live?plan=…`), each event carries the title and the dynamics as JSON instead of the body's HTML, and the page ends equal to the server's HTML for that state; a stream without a matching fingerprint keeps sending the body's HTML.

### SSR-31: Navigations in the same layout replace only the page

- **Given** two pages in the same layout
- **When** the navigation script goes from one to the other
- **Then** it sends its layout signature, receives the page alone, replaces only the page inside the layout (the layout's elements stay untouched), updates the title, the URL and the page's metadata, and marks the link to the new page as current.

### SSR-33: Commands navigate without reloading the page

- **Given** a page whose command navigates with `INavigator`
- **When** the navigation script submits it
- **Then** it follows the `303` with `fetch` and the page does not reload: to another page, it shows that page and pushes its URL; to the same page with another query (`?saved=1`), it receives the dynamics as JSON, morphs them in and pushes the new URL.

### SSR-65: Commands that leave the site run once

- **Given** a page whose command navigates to another site (`RedirectTo` or `INavigator` with an absolute URL of another origin, SSR-37)
- **When** the navigation script submits it
- **Then**
  - The server answers the script's request (`X-Aioros-Navigation`) with `200`, no `Location` and the URL in `X-Aioros-Redirect`, the script goes there with `location.href`, and the command has run exactly once; a request without the header still gets `303` (SSR-37).
  - A `fetch` that fails is never submitted again natively, since its command may already have run.

*Precedent: Turbo's `FetchRequest.perform` reports a failed fetch to `FormSubmission.requestErrored`, and the navigator only logs it (`formSubmissionErrored`), never resubmitting.*

### SSR-76: A view command morphs only its region

- **Given** a page with a heading and a view whose button counts, in a browser
- **When** the button is clicked
- **Then** the navigation script posts the form, receives the region alone and morphs only `[data-aioros-view="key"]`: the page is not reloaded, an attribute set on the heading outside the region stays, and the region then equals the server's HTML for the same state.

### SSR-82: A command morphs every region its keys refreshed

- **Given** a page with a heading and two views, a list that reads `items` and a form whose button adds an item through a function invalidating `items`, in a browser
- **When** the button is clicked
- **Then** the navigation script receives both regions in one answer and morphs each `[data-aioros-view]` in place: the list shows the new item, an attribute set on the heading outside the regions stays, the page is not reloaded, and both regions then equal the server's HTML for the same state.

### SSR-118: Invalidated views arrive as regions in place

- **Given** a live page in the browser whose view reads `sales`
- **When** `sales` is invalidated on the server
- **Then** the script's stream receives the view's region and morphs it in place: the view shows the fresh value, the rest of the page and its state (a value set by script) are untouched.

### SSR-298: A press while a post is pending is ignored

- **Given** a page in the browser whose post is still waiting for its answer
- **When** the visitor presses a button of the form again (the same one or another)
- **Then** the press is ignored: no second request is sent, the browser does not submit the form natively (the page stays, with no prompt to leave it), and the first post's answer is applied when it arrives

*Precedent: Phoenix LiveView disabling a form's submit buttons until its submit is acknowledged; Turbo ignoring a form's submits while one is in flight.*

## Navigator

### SSR-32: Commands navigate with see other

- **Given** a page whose command asks the injected `INavigator` (scoped to the request) to go to a path
- **When** the page is posted to
- **Then** the answer is `303` with that path in `Location` and nothing is rendered, also when the post comes from the navigation script with `X-Aioros-Plan` (it never answers with dynamics instead).

*Precedent: Remix and React Router `redirect` from an action; Uno.Extensions `INavigator.NavigateAsync(NavigationRequest)`.*

### SSR-34: Navigations while loading redirect

- **Given** a page or layout model that asks `INavigator` to navigate while it is created or loaded (its constructor or `LoadAsync`)
- **When** the page is requested
- **Then** the answer is `302` to that path without rendering, as with `RedirectTo` (SSR-12)
- **When** it is posted to
- **Then** the answer is `303`.

### SSR-35: Relative paths resolve against the page

- **Given** a navigation to a relative path
- **When** it is answered
- **Then** the path is resolved against the page's URL as a browser resolves a relative link on it (RFC 3986, section 5): from `/crm/list`, `edit` is `/crm/edit`, `../about` is `/about` and `?page=2` is `/crm/list?page=2`.

*Precedent: a relative `Location` header; MAUI Shell's relative routes.*

### SSR-36: The navigator wins over redirect to

- **Given** a model that sets `RedirectTo` (the page's or a layout's) and a navigation asked of `INavigator` in the same request
- **When** the request is answered
- **Then** the navigator's path wins, because `RedirectTo` is the deprecated fallback and a state that can be left over, while a navigation is an explicit request; of several navigations, the last one wins; without a navigation, `RedirectTo` still redirects (SSR-08, SSR-12, SSR-23).

### SSR-37: Other sites are allowed and other schemes refused

- **Given** a navigation to an absolute `http` or `https` URL of another site
- **When** it is answered
- **Then** `Location` is that URL as given, as a `RedirectTo` to another site is and as Next.js `redirect` allows
- **When** a URL with another scheme (`javascript:`, `data:`)
- **Then** `NavigateAsync` throws `ArgumentException` and nothing is recorded, so the request fails instead of redirecting.

### SSR-66: The apps own navigation interface redirects

- **Given** a page whose view model takes the app's own navigation interface registered with `AddNavigation` (GENERATOR-35) and whose layout's view model takes `INavigator`
- **When** a command navigates through the app's interface
- **Then** the answer is `303` to that path (as SSR-32)
- **When** the view model navigates through it while it is created
- **Then** the answer is `302` (as SSR-34); and when the layout's view model also navigates through `INavigator` in the same request, both go through the same scoped navigator, so `INavigator` keeps working and the last navigation asked for wins.

## Loader

### SSR-38: Loaders receive the request and feed the view model

- **Given** a page with a loader
- **When** it is requested with route values, a query string, a signed-in user and cookies (`sidebar_state=false`)
- **Then** the loader receives the path, the route values, the query, the user and the cookies by their exact name (`PageRequest.Cookies`, empty on the native targets), and the page's view model is created with the loader's data and rendered from it.

*Precedent: Remix and React Router `loader({ request, params })`, SvelteKit `load({ params, url, locals, cookies })`, Next.js's `cookies` in a layout (shadcn/ui's sidebar reads `sidebar_state` there for its `defaultOpen`).*

### SSR-39: Page and layout loaders run in parallel

- **Given** a page and its layout, each with a loader
- **When** the page is requested
- **Then** both loaders are started before either completes (the layout's still pending when the page's starts), and the models are created, outermost first, only once both have completed.

*Precedent: Remix and SvelteKit run the loaders of every matched route in parallel; a loader cannot read its parent's data (SvelteKit's `await parent` makes waterfalls).*

### SSR-40: Pages without loaders allocate only their models array

- **Given** a page and a layout without loaders whose factories return existing models
- **When** their models load
- **Then** the only allocation is the array that holds the models, so a page without loaders pays nothing for them; and revalidating that page after a command (SSR-52) allocates nothing, since it has no loader to run again.

### SSR-41: Loaders that throw reach the error page

- **Given** a page whose loader throws
- **When** it is requested from an app with the exception handler
- **Then** the loader's own exception (not an aggregate) reaches the handler, the answer is 500 with the app's error page and the page's view model is never created.

### SSR-42: Loaders that navigate redirect without models

- **Given** a page whose loader asks the injected `INavigator` to go to a path
- **When** the page is requested
- **Then** the answer is `302` to that path without rendering and no view model of the page or its layouts is created, as Remix's `redirect` from a loader skips the route's component.

### SSR-43: Posts reload through the loader before binding

- **Given** a page with a loader
- **When** a form is posted to it
- **Then** the loader runs again before binding and the form's fields are bound into the view model created with that fresh data, as the post reloads every model (SSR-08).

### SSR-84: Key bookkeeping costs nothing without keys

- **Given** a page with a loader that reads no keys
- **When** it is loaded in an app whose in-process callers and `IDataKeys` are registered and the loader takes `IDataKeys` without reading
- **Then** loading allocates exactly what it allocates without any key registration: key bookkeeping costs nothing on a page without keys.

## Loader result

### SSR-44: Loaders that find nothing answer the not found page

- **Given** a page whose loader, or the loader of one of its layouts, returns `LoaderResult.NotFound`
- **When** the page is requested or posted to
- **Then** the answer is 404 with the app's not-found page and its layouts (SSR-16), no view model of the page or its layouts is created and nothing is bound.

*Precedent: React Router and Remix `throw data(null, { status: 404 })` from a loader, SvelteKit `error(404)` in `load`.*

### SSR-45: Loaders that redirect answer without models

- **Given** a page whose loader, or a layout's, returns `LoaderResult.Redirect(path)`
- **When** the page is requested
- **Then** the answer is `302` to that path, resolved against the page's URL as SSR-35 resolves a navigation, without rendering and without creating any view model
- **When** it is posted to
- **Then** the answer is `303` before anything is bound; a scheme other than `http` or `https` fails the request as SSR-37.

*Precedent: React Router and Remix `redirect` from a loader, SvelteKit `redirect` in `load`.*

### SSR-46: Redirects win and the outermost loader first

- **Given** loaders that disagree
- **When** the page is requested
- **Then** a redirect wins over a not found, the outermost loader's redirect wins over a deeper one's (the layout's over the page's), a loader's redirect wins over a navigation asked of `INavigator` while the page loads, and that navigation wins over a loader's not found; a page model's `NotFound` and `RedirectTo` (SSR-04, SSR-12) are read only when no loader stopped the page and nothing navigated.

*Precedent: React Router's `findRedirect` takes the first redirect in match order (root first) before any error is processed; SvelteKit awaits the nodes' server loads in order, so the outermost failure answers.*

### SSR-47: Loaders give the title and metadata

- **Given** a page whose loader returns its data with a title and metadata (`new LoaderResult<T>(data) { Title = …, Metadata = … }`)
- **When** the page is requested
- **Then** the document's `<title>`, meta description, canonical link and Open Graph tags come from the loader; a navigation request (SSR-22) carries that title, the dynamics answer (SSR-26) carries it in `t`, and so does every event of its live stream (SSR-28).

*Precedent: SvelteKit returns the title in the data of `load`; Remix's `meta({ data })` and Next.js `generateMetadata` derive it from the loaded data.*

### SSR-48: Page models still give what loaders leave out

- **Given** a page whose loader returns only its data and whose view model still implements `IPageModel` with a title, metadata or `NotFound`
- **When** the page is requested
- **Then** the view model's values are used as before
- **When** the loader also returns a title or metadata
- **Then** the loader's win, field by field (title and metadata apart).

## Loader scope

### SSR-49: Each loader has its own scope

- **Given** a page and its layout whose loaders each take the same scoped service, one that refuses a second operation while one is in progress (as an EF Core `DbContext` does)
- **When** the page is requested and both loaders are inside that service at once
- **Then** each loader has its own instance, created in a DI scope of its own from the request's `IServiceScopeFactory`, and the page renders with both loaders' data.

*Precedent: Blazor's `OwningComponentBase` gives each component its own scope for exactly this `DbContext` problem (learn.microsoft.com/aspnet/core/blazor/fundamentals/dependency-injection, "Utility base component classes to manage a DI scope".*

### SSR-50: Each loader scope is disposed when its loader completes

- **Given** a page and its layout with loaders
- **When** each loader completes
- **Then** its scope is disposed as soon as it completes, its async-disposable services through `DisposeAsync`
- **When** one loader throws
- **Then** its scope is disposed too, the other's is disposed when it completes, and the request fails with the loader's exception (SSR-41).

### SSR-51: View models keep the request scope

- **Given** a page whose loader and view model both take a scoped service
- **When** the page is requested
- **Then** the view model gets the request scope's instance (`HttpContext.RequestServices`), never the loader's, and the loader still reaches the request: the user through `PageRequest.User`, the current `HttpContext` through `IHttpContextAccessor`, and the page's `INavigator` (SSR-42), which is the request scope's.

## Revalidation

### SSR-52: Successful commands run the loaders again

- **Given** a page and its layout, each with a loader, whose button runs a command that completes without an exception and after which no model reports `INotifyDataErrorInfo.HasErrors`
- **When** the form is posted
- **Then** both loaders run a second time after the command, each in a new DI scope of its own (SSR-49), and the answer is rendered from the models holding that fresh data: the HTML page, and the JSON dynamics when the post carries the plan's fingerprint (SSR-27); a post without a command (no button) runs no loader again.

*Precedent: React Router revalidates every active loader after an action (`shouldRevalidate`'s default), SvelteKit reruns `load` after a form action.*

### SSR-53: View models that take data are kept and others created again

- **Given** a page whose view model takes new data through a public `Receive(TData)` method (GENERATOR-23) and whose command sets a property (a "Saved" message)
- **When** the post's command succeeds
- **Then** the same view model instance receives the loader's fresh data and the answer shows both the message and the fresh data
- **When** a layout or page whose view model does not take new data
- **Then** a new view model is created from the fresh data (the view model the command ran on is dropped, as the post's state does not outlive the request).

*Precedent: SvelteKit keeps the component and updates its `data` prop; Prism `IsNavigationTarget` keeps a view model that accepts the navigation.*

### SSR-54: Failed commands revalidate nothing

- **Given** a page with a loader
- **When** a posted command throws
- **Then** no loader runs again and the exception reaches the app's exception handler (500, SSR-41)
- **When** the command completes but a model reports `HasErrors` (field errors through `INotifyDataErrorInfo`)
- **Then** no loader runs again and the page is rendered from the view model the form was bound into, so its field errors follow their fields (NAVIGATION-13).

*Precedent: React Router skips revalidation when an action returns or throws a 4xx or 5xx response; field errors map to 422 and an exception to 500.*

### SSR-55: Navigating commands skip revalidation and fresh results answer

- **Given** a page with a loader whose command navigates (with `INavigator` or the deprecated `RedirectTo`)
- **When** it is posted to
- **Then** the answer is `303` to that path and no loader runs again, since the next page loads its own data
- **When** a successful command after which the revalidating loader answers `LoaderResult.NotFound` or `LoaderResult.Redirect(path)` (the record was deleted or moved)
- **Then** the answer is 404 with the not-found page or `303` to that path (SSR-44, SSR-45).

### SSR-153: Posts that show what they left are answered as rendered

- **Given** a page with a loader whose post outcome reports that the answer shows what the post left for the user (a prompt or a notice)
- **When** a page's or a view's button is posted
- **Then** the answer is the post's own render as a whole page and the loaders do not run again, while the same post without that report revalidates; a post holding a `_prompt` field goes to the engine even when the page's plan is cached.

## Page data

### SSR-56: Data requests answer every segments loader results

- **Given** a page and its layout whose loaders return data with a title and metadata
- **When** `GET /_data{route}` is requested with the page's data contract hash in `X-Aioros-Contract`
- **Then** the answer is `200` `application/json` with `Cache-Control: no-store` and `{"type":"data","segments":[…]}`: one entry per segment, layouts first and the page last, each with its loader's data written by the generated writer (GENERATOR-27), its `title` and its `metadata` when it set them, and `null` for a segment without a loader; the loaders run as for the page (in parallel, each in a scope of its own, SSR-49) and no view model is created.

*Precedent: Remix single fetch (`/route.data`) and SvelteKit's `__data.json` answer one request with the data of every matched route, layouts first.*

### SSR-57: Data requests carry the route query and user to the loader

- **Given** a page `/issues/{id}` whose loader reads its request
- **When** `/_data/issues/7?tab=open` is requested by a signed-in user
- **Then** the loader receives the page's path `/issues/7` (without `/_data`), the route value `id` `7`, the query value `tab` `open` and the user.

### SSR-58: Not found and redirects are answered for the client

- **Given** a page whose loader, or a layout's, answers `LoaderResult.NotFound`
- **When** its data is requested
- **Then** the answer is `404` with `{"type":"notFound"}`, and so is a path under `/_data` that matches no page
- **When** the app has a not-found page, the answer also carries its `contract` and its `segments` loaded for that path as in SSR-56, so the installed app renders it without another request (DESKTOP-34); given a loader's `LoaderResult.Redirect(path)` or a navigation asked of `INavigator` while the loaders run
- **Then** the answer is `200` with `{"type":"redirect","location":…}`, the location resolved against the page's path as SSR-45 and chosen in SSR-46's order, never a `3xx`, which `HttpClient` and `fetch` would follow and lose.

*Precedent: SvelteKit's `__data.json` answers `{"type":"redirect","location"}` with `200`; Remix single fetch encodes a loader's redirect in the payload for the same reason.*

### SSR-59: Data requests follow the pages access rules

- **Given** a page whose access rules (a loader's `[Authorize]` or a folder convention, ACCESS-02, 05) require a signed-in user and a policy
- **When** its data is requested anonymously
- **Then** the answer is `401` without a `Location`, even when the cookie scheme sends pages to its login path; signed in without the policy, `403`; satisfying it, `200`; a page with `[AllowAnonymous]` or `AllowAnonymousToPage` answers `200` anonymously; no loader runs on a `401` or `403`.

*Precedent: ASP.NET Core 10's cookie authentication answers `401` and `403` instead of redirecting for API endpoints; server functions answer the same (FUNCTIONS-04).*

### SSR-60: Another data contract is answered with upgrade required

- **Given** a data request whose `X-Aioros-Contract` is missing or differs from the page's data contract hash (GENERATOR-28)
- **When** it is requested
- **Then** the answer is `426` with the server's hash in `X-Aioros-Contract`, before any access check and without running a loader, as server functions answer (FUNCTIONS-03).

### SSR-61: Loader failures answer problem details without their details

- **Given** a page whose loader throws
- **When** its data is requested
- **Then** the answer is `500` `application/problem+json` with a generic title and the status, never the exception's message or stack trace, and the exception is logged.

### SSR-72: Data requests carry each view by its key

- **Given** a page with view instances
- **When** the installed app asks `/_data{route}`
- **Then** the answer carries a `views` object keyed by instance key with each loaded view's `data`, `{"state":"failed"}` for a view whose loader threw and `{"state":"hidden"}` for one the visitor may not see, and reading the answer back fills the view slots with the data or the markers.

### SSR-77: Data requests for one view run only its loader

- **Given** a page with view instances
- **When** the installed app asks `/_data{route}` with `X-Aioros-View: {key}`
- **Then** only that view's loader runs and the answer carries `segments` empty and `views` with that instance alone (its `data`, or `{"state":"hidden"}` or `{"state":"failed"}`); a key that names no instance with a loader answers `404`, and the page's access rules and data contract are checked as for the whole page (SSR-72).

### SSR-83: Data answers carry the keys each loader read

- **Given** a page whose page loader and a view read keys
- **When** `/_data{route}` is requested
- **Then** each segment and each view carries `keys` with the keys its loader read, and a loader that read none carries no `keys` member; a request for one view (`X-Aioros-View`, SSR-77) carries that view's keys too.

## Container

### SSR-62: Another container serves pages posts and functions

- **Given** a web app built with `UseServiceProviderFactory` and another container (Autofac), with the framework's page services added through `AddAiorosPages` and a server function through `AddServerFunction`
- **When** a page with a loader is requested, the page is posted to with a successful command and the server function is called
- **Then** the app's services are that container's, the page answers `200` with its loader's data, the post runs the loader again in a new scope of that container and answers with the revalidated data, and the server function answers its result; nothing needs Microsoft's container.

### SSR-63: A scoped messenger is shared within a request only

- **Given** an `IMessenger` registered as scoped and a page and its layout whose view models both take it
- **When** the page is requested twice
- **Then** within one request both view models get the same messenger, so a message one sends reaches the other, and the second request gets another messenger, so no message crosses requests.

*Precedent: the CommunityToolkit.Mvvm messenger documentation (one messenger instance per scope instead of `WeakReferenceMessenger.Default`).*

### SSR-64: Development rejects a singleton view model that captures the navigator

- **Given** the web host's builder in the `Development` environment with the framework's page services and a page view model registered as a singleton that takes `INavigator`
- **When** the app is built
- **Then** building fails because the singleton would capture the request's scoped navigator (`ValidateOnBuild`), and resolving `INavigator` from the root fails (`ValidateScopes`); in `Production` the same app builds.

*Precedent: ASP.NET Core turns both validations on in Development.*

## View loader

### SSR-67: View loaders run in parallel with the pages in their own scopes

- **Given** a page and its layout with loaders and two view instances with loaders
- **When** the page is requested
- **Then** all four loaders have started before any completes, each in its own scope, each view model is created from its own loader's data in the slots after the page's model in instance order, and the page's and layout's models are those they would be without the views.

*Precedent: Relay combines the fragments of a page into one request; Phoenix LiveComponents load per component id.*

### SSR-68: Only the page and its layouts name the page

- **Given** an embedded view whose loader sets a title and metadata, and another whose loader answers not found or a redirect
- **When** the page is requested
- **Then** the page answers `200` with the title and metadata of the page and its layouts only, the view that stopped renders nothing, and the page's own not found and redirect still answer `404` and `302`.

### SSR-69: Views the visitor may not see render nothing

- **Given** an embedded view whose loader has `[Authorize(Roles = "admin")]`
- **When** an anonymous visitor or a signed-in visitor without the role requests the page
- **Then** its loader never runs, its slot holds the hidden marker and the page answers `200` without the view
- **When** an admin requests it
- **Then** the view is loaded and rendered.

### SSR-70: A failing view renders its error while the page answers

- **Given** an embedded view whose loader throws
- **When** the page is requested
- **Then** the page answers `200`, the view's region renders as its error (RENDERING-15) and the exception is logged, while a page or layout loader that throws still fails the page (SSR-41).

*Precedent: Next.js `error.js` per route segment and React error boundaries.*

### SSR-71: Views without loaders get their view model from dependency injection

- **Given** a view without a loader whose view model is registered in DI
- **When** the page is requested twice
- **Then** each request's view slot holds the view model the request's provider returns.

## View command

### SSR-73: A view command revalidates only that view

- **Given** a page and its layout with loaders and two instances of the same view with loaders, each loader counting its runs
- **When** the second instance's button is posted and its command succeeds
- **Then** only that instance's loader runs again, once, in a new DI scope, its view model receives the fresh data (`Receive`) or is created from it, and the page's, the layout's and the first instance's loaders ran only to load the request
- **When** the command throws, leaves a model with `HasErrors` or navigates (`303`)
- **Then** no loader runs again; a page button still runs every loader again (SSR-52).

*Precedent: LiveView `phx-target={@myself}`, where only the component handles its event.*

### SSR-74: A view command from the script answers only its region

- **Given** a reusable page with a view
- **When** the navigation script posts a command of that view (`X-Aioros-Navigation: 1`, with or without `X-Aioros-Plan`)
- **Then** the answer is `text/html` marked `X-Aioros-View: {key}` whose body is only that view's region, the same bytes as that region in the page rendered from the same models, without the page's statics or other regions
- **When** the same form is posted without the script
- **Then** the whole page is answered; a page command answers as before (SSR-27), and a view whose region is not on the page is answered with the whole page.

*Precedent: Turbo Frames (a form inside a frame replaces only that frame), LiveView diffs per component.*

### SSR-75: The antiforgery token stays valid across region answers

- **Given** antiforgery tokens turned on (SSR-15) and a page with a view
- **When** the navigation script posts a command of the view with the form's token
- **Then** it is accepted, the region answer issues no new token (the form's token stays in place outside the region), and a second post with the same token is accepted; a post of the view's command without a token answers `400` and runs nothing.

### SSR-109: A changed selection answers its views region

- **Given** a page with views
- **When** the navigation script posts a changed selection (a post without a button whose `_chg` names a field of the second view)
- **Then** that view's fields are bound, no command and no loader runs again, and the answer is that view's region alone (`X-Aioros-View` with its key, rendered from what the post bound); the same post without the script gets the whole document, and a changed field of the page itself is answered with the whole page (its dynamics for the script).

## View key

### SSR-78: Each loader keeps the keys it read

- **Given** a page whose layout, page and three view loaders run in parallel
- **When** the first view's loader calls a server function declared `[Reads("issues")]` (through the in-process caller it takes) and the second calls `IDataKeys.Read("issue:7")`
- **Then** the loaded page holds, per slot, the keys each loader read in its own scope (`issues` for the first view, `issue:7` for the second, none for the others), and no loader sees another's keys.

### SSR-79: A command reruns the views whose keys it invalidated

- **Given** a page and layout with loaders and four views (A reads `issues`, B reads `other`, D reads `issue:7`, E reads `issue:8`), each loader counting its runs
- **When** a command in a fifth view C succeeds after a server function invalidated `issues` and `issue:7`
- **Then** only C's, A's and D's loaders run again (once each, after the request's own load) and B's, E's, the page's and the layout's do not; when the page itself reads `issues`, the page's and its layout's loaders run again with them, the others' views do not, and the answer is the whole page.

### SSR-80: Unknown keys refresh the whole page and no call only its view

- **Given** the page of SSR-79
- **When** the command in C called a server function without key information
- **Then** every loader of the page runs again and the whole page is answered, the safe default
- **When** it called no server function
- **Then** only C's loader runs again (SSR-73)
- **When** it failed, left `HasErrors` or navigated
- **Then** no loader runs again, whatever it invalidated.

### SSR-81: The script gets every refreshed region in one answer

- **Given** the page of SSR-79 posted by the navigation script
- **When** C's command invalidates `issues`
- **Then** the answer is `text/html` marked `X-Aioros-View` with the keys of A and C in document order, separated by commas, and its body is A's region followed by C's, each the same bytes as in the page rendered from the same models; without the script the whole page is answered.

## Repeated view

### SSR-85: A batch loader loads every item of the list once

- **Given** a page whose loader returns three items and whose list's item template places a view with a batch loader keyed by the item's id
- **When** the page is requested
- **Then** the batch loader runs once, after the page's loader, in its own scope, with the distinct keys in list order, and each item's view model is created from the data the loader returned for its key; a key the loader returns nothing for hides that item; a batch loader that throws marks every item failed, logged, while the page answers `200`; a batch loader the visitor's rules refuse never runs and the items render nothing.

*Precedent: DataLoader (one batch per tick with the distinct keys), Phoenix LiveComponent `update_many/1`.*

### SSR-86: A loader for one instance runs once per item

- **Given** a view with a per-instance loader placed in a list of three items (the N+1 shape, GENERATOR-47)
- **When** the page is requested
- **Then** its loader runs once per item, each in its own scope, and each item's view model is created from its own run.

### SSR-87: An items command refreshes that item through its batch loader

- **Given** that page with a button in each item's view
- **When** the navigation script posts the third item's button and its command succeeds
- **Then** the batch loader runs again with the third item's key alone, only that item's view model receives the data or is created again, the others are the same instances, and the answer is that item's region alone marked `X-Aioros-View: {key}[{item}]`
- **When** the command invalidated a key the batch loader read
- **Then** it runs again with every key and the answer carries every item's region in list order.

### SSR-88: Data requests carry each item of a repeated view

- **Given** that page
- **When** the installed app asks `/_data{route}`
- **Then** the instance carries `items`, an array of `{"key": …, "data": …}` in list order (or `"state"` for a hidden or failed item), with its loader's `keys`
- **When** it asks with `X-Aioros-View: {key}[{item}]`
- **Then** the batch loader runs with that item's key alone, parsed from its text, and the answer carries that item alone.

## Memo

### SSR-89: Loaders of one load share a memoized call

- **Given** a page loader and a view loader that each call a `[Memo]` server function with equal arguments in one load
- **When** the page is requested
- **Then** the implementation runs once and both loaders get the same result, while each loader keeps its own scope; with other arguments it runs once per argument set; the next load (another request, or the revalidation after a command) runs it again; a failing call is shared by the loaders of that load only and the next load calls it again; a command's call is never memoized.

*Precedent: React `cache`, one memo per server request whose entries, failures included, end with it.*

## Loader limit

### SSR-90: Loaders run at most the limit at once

- **Given** a page with twelve view loaders that each signal when they start and wait to be released
- **When** the page is requested with the default limit
- **Then** eight have started and the ninth starts only when one of them completes, and every loader runs to the end; with the limit set to two, two at a time; with a global limit of three shared by two requests, three at a time across both; a page with no more loaders than the limit allocates what it allocated without a limit.

*Precedent: .NET's `ConcurrencyLimiter`, sized as a connection pool.*

## Document write

### SSR-91: Large pages grow no array and allocate nothing per row

- **Given** a page whose body renders to more than 85 KB (the large object heap's threshold) from a list of 3,000 rows
- **When** it is requested again once its plan is cached
- **Then** the request allocates less than a quarter of the body's size: the body and the document's start are written into pooled buffers, the response is started with its length and gets the body copied once, and a list's rows share the scopes around them, so a large page grows no array and allocates nothing per row.

*Precedent: Kestrel and System.Text.Json write through pooled buffers (`ArrayPool<byte>.Shared`); a response with its `Content-Length` known is sent without being buffered again.*

### SSR-189: Firefox scrollbars follow the color scheme

- **Given** a page rendered in the dark theme (its root's `color-scheme` is `dark`)
- **When** Firefox draws a scroll container's classic scrollbar (a sidebar's)
- **Then** the document's base styles give Firefox `scrollbar-color` from the scheme (`light-dark`, a translucent thumb on a transparent track) inside `@supports (-moz-appearance:none)`, so the track is not a light strip beside dark content, while Chromium keeps its own scrollbars.

*Precedent: CSS Scrollbars Styling (`scrollbar-color`) and `light-dark`, Firefox's way to theme classic scrollbars, which its non-native scrollbars otherwise draw light in a dark page.*

## Cross site

### SSR-92: Same origin posts run their command

- **Given** a page with a form
- **When** a post arrives with `Sec-Fetch-Site: same-origin`, or `none` (typed in the address bar, a bookmark)
- **Then** the command runs and the page is answered as usual (`200`), as Go's `net/http.CrossOriginProtection` allows them.

### SSR-93: Cross site posts are refused

- **Given** a page with a form
- **When** a post arrives with `Sec-Fetch-Site: cross-site`, whatever its `Origin`
- **Then** it is answered `403` before the form is read, and no command runs.

### SSR-94: Same site posts from a sibling subdomain are refused

- **Given** a page served on `app.test`
- **When** a post arrives from a sibling subdomain with `Sec-Fetch-Site: same-site` and `Origin: http://evil.app.test`
- **Then** it is answered `403` and no command runs: a sibling subdomain is another origin, as Go rejects every value other than `same-origin` and `none`.

### SSR-95: Without fetch metadata the origin must name the host

- **Given** a page served on `app.test`
- **When** a post arrives without `Sec-Fetch-Site` (an older browser, or a plain HTTP origin where browsers send no fetch metadata)
- **Then** it is accepted when its `Origin` names the request's host and port (`http://app.test`), and answered `403` with no command run when the `Origin` names another host (`http://evil.test`), another port (`http://app.test:8080`) or is `null`.

### SSR-96: Posts without fetch metadata or origin run their command

- **Given** a page with a form
- **When** a post arrives with neither `Sec-Fetch-Site` nor `Origin` (a non-browser client such as the installed app, `curl` or a load generator)
- **Then** the command runs: a request a browser did not send cross-site cannot carry a visitor's cookies against their will, as Go allows it.

### SSR-97: Trusted origins and bypassed routes are accepted

- **Given** a `CrossOriginProtection` registered with `TrustedOrigins = ["https://admin.test"]` and `InsecureBypassPatterns = ["/hooks"]` (Go's `AddTrustedOrigin` and `AddInsecureBypassPattern`)
- **When** a post to a page arrives with `Sec-Fetch-Site: cross-site` and `Origin: https://admin.test`
- **Then** the command runs; a cross-site post from another origin to that page is `403`; a cross-site post to the `/hooks` page runs its command; and a trusted origin with a path, a query or no scheme is refused with `ArgumentException` when it is set.

### SSR-98: Safe methods are never checked

- **Given** a page with a loader
- **When** it is requested with `GET` and `Sec-Fetch-Site: cross-site` and `Origin: http://evil.test`, or its data with `GET /_data{route}` from the same cross-site request
- **Then** both are answered `200`: safe methods are never checked, since they change nothing (`HEAD` and `OPTIONS` likewise).

### SSR-99: Pages carry no antiforgery token by default

- **Given** a page with form fields and ASP.NET Core antiforgery registered by something else (MVC, Razor components)
- **When** it is rendered and then posted from the same origin without a token
- **Then** the page carries no `__RequestVerificationToken` field, no antiforgery cookie is set, and the post's command runs: the fetch metadata check replaces the token, which an app turns on with `AddAntiforgeryTokens` (SSR-15).

## Page cache

### SSR-100: Pages that do not depend on the user are served from the cache

- **Given** a page whose layout and page loaders are `[Cached]` (a lifetime on each `LoaderDefinition`), with no access rules and not live
- **When** it is requested twice with the same host, path, query and theme
- **Then** the second answer is the same bytes and its loaders do not run again; another query or theme is a separate entry; the navigation script's partial answer and its plan dynamics are kept and served the same way; and each answer carries `Cache-Control: public, no-cache` with `Cookie` in `Vary`.

### SSR-101: Cached pages are evicted by key lifetime and plan

- **Given** a cached page whose page loader read the key `issues`
- **When** a post's command invalidates `issues`
- **Then** the next request runs the loaders again and shows the fresh data; invalidating another key keeps the entry; when the shortest lifetime of its loaders passes on a fake `TimeProvider`, the loaders run again; and when its plan is replaced (a hot reload clears the plan cache), the entry is not served.

### SSR-102: Pages without a declared lifetime are never kept

- **Given** pages where one slot's loader has no lifetime, a layout has no loader, the page is live, or a loader answers not found
- **When** each is requested twice
- **Then** its loaders run both times, nothing is kept, and a page that renders answers `Cache-Control: private, no-cache`.

### SSR-103: User pages are never cached for a cookie bearing request

- **Given** a page whose loaders all have a lifetime but which has access rules (its loader's `[Authorize]` or a folder convention)
- **When** a signed-in user requests it twice with an authentication cookie
- **Then** the loaders run both times, nothing is kept or served from the page cache, and the answer is `Cache-Control: private, no-cache` with no `public`.

### SSR-104: Every page answer carries a strong tag and revalidates

- **Given** a page that is not cached
- **When** it is requested
- **Then** the answer carries a strong `ETag` (a quoted hash of the bytes written and the request's `Accept-Encoding`), equal for equal bytes and different when they differ
- **When** it is requested again with that tag in `If-None-Match` (alone, in a list, weak-prefixed `W/`, or `*`)
- **Then** it is answered `304` with the tag, `Cache-Control` and no body; a post's answer carries no tag and `Cache-Control: no-store`.

### SSR-105: Cached pages revalidate without loading or rendering

- **Given** a cached page
- **When** it is requested with its tag in `If-None-Match`
- **Then** it is answered `304` without its loaders running or the page rendering; the document, the navigation script's answer, its partial answer and the plan dynamics each have their own tag.

### SSR-106: A page cache of no bytes keeps nothing

- **Given** a page cache limited to 0 bytes (`Aioros:PageCacheBytes=0` in the app's configuration)
- **When** a page whose loaders all declare a lifetime is requested twice
- **Then** its loaders run both times, nothing is kept, and the answers still carry their tag and `Cache-Control: public, no-cache`.

### SSR-218: Kept answers are per culture

- **Given** a page of the app of SSR-214 whose loader keeps its answer for a lifetime (the page cache)
- **When** it is answered in `fr-FR` and then requested in `en-US`
- **Then** the `en-US` request is rendered, not served the kept `fr-FR` answer, and a second `fr-FR` request is served from the cache.

### SSR-231: Pages reading cookies are never shared

- **Given** an app that keeps pages and a page whose loader reads `PageRequest.Cookies` (so it declares no lifetime)
- **When** it is requested with `sidebar_state=false` and then with `sidebar_state=true`
- **Then** each answer is written by the loader from its own cookie, neither is kept nor served from a kept answer, and each carries `Cache-Control: private, no-cache` (no shared cache stores it) and `Vary: Cookie` (no cache reuses it for other cookies); a page whose loaders all declare a lifetime keeps `public, no-cache` with `Vary: Cookie`.

*Precedent: Next.js's `cookies` making a route dynamic (`private, no-cache, no-store`), RFC 9111 §4.1 (`Vary`).*

## Page head

### SSR-108: Form controls inherit the font but not the line height

- **Given** a page
- **When** its document is written
- **Then** the base styles give buttons, inputs, text areas and selects their parent's font family, size, weight and style one property at a time and never through the `font` shorthand, which would also reset their `line-height` and undo the line height the page sets on every element (TEXT-16); they set no `line-height` of their own.

## Change script

### SSR-110: A changed selection is posted and its answer shown

- **Given** a page with a selection group marked `data-change` and a `<select>` that is not
- **When** a visitor changes the select and then picks another radio in the group in a browser
- **Then** the framework's script posts the form once, for the group, with `_chg` naming the changed field's token and every field's value (the select's included), and shows the answer in place without reloading (the text bound to the selection reads the new value); the unmarked select posts nothing, and without JavaScript picking a radio posts nothing either.

## Page code

### SSR-111: An async void handler that throws fails only its own request

- **Given** the development engine and a page whose code-behind has an `async void` handler that throws on Avalonia's thread while the page renders (an `OnApplyTemplate` awaiting `Dispatcher.UIThread.InvokeAsync`, as Semi.Avalonia's `/palette` does)
- **When** it is requested
- **Then**
  - That request is answered 500 with the error page, the exception logged for it names the route and carries the handler's exception, and the next request, to another page, is answered 200 by the same server.
  - Avalonia's dispatcher reports the exception (`Dispatcher.UIThread.UnhandledException`), the engine marks it handled and fails only the render it was raised in.

*Precedent: WPF's `Dispatcher.UnhandledException` with `Handled = true`, Blazor Server, which tears down only the failing circuit, and ASP.NET Core, which answers 500 per request.*

### SSR-112: Page code that throws after the answer is logged and contained

- **Given** the development engine and a page whose code-behind starts an `async void` that throws after the page was answered (fire-and-forget)
- **When** it throws
- **Then**
  - The server logs it as the page's code failing after its answer, naming the route, and keeps serving: the next request is answered 200.
  - Without the engine's handler the exception leaves Avalonia's main loop and ends the process.
  - An exception on a thread the app starts itself, or in an `async void` started where there is no synchronization context, reaches the thread pool and ends the process in .NET; it cannot be contained and is not.

### SSR-113: Models the engine renders are created on avalonias thread

- **Given** the development engine and a page with no plan yet whose view model builds an Avalonia brush in its constructor (as Semi.Avalonia's `/highcontrasttheme` does)
- **When** it is requested
- **Then** its view model is created on Avalonia's UI thread, where the engine renders it (the brush's `CheckAccess` is true there), and the page is answered 200
- **When** it is requested again with its reusable plan cached
- **Then** the model is created on the request's thread and the engine is not involved.

*Precedent: Avalonia's thread affinity (`AvaloniaObject.Dispatcher` is the creating thread's, and `VerifyAccess` throws on any other), and the desktop target, which creates its models on the UI thread.*

### SSR-114: Async void model code on a planned page fails only its request

- **Given** a page served from its cached plan without the engine (production)
- **When** its view model's constructor starts an `async void` that throws before its first `await`
- **Then** the request is answered 500 with the exception logged and the next request is answered 200
- **When** such an `async void` throws after the answer
- **Then**
  - It is logged as the page's code failing after its answer and the server keeps serving.
  - Model creation on a request thread runs under a synchronization context of the page's code, so an `async void` reports to it instead of the thread pool; the engine is never called and code-behind never runs.

*Precedent: ASP.NET's `AspNetSynchronizationContext` and Blazor's `RendererSynchronizationContext`.*

## Live key

### SSR-115: Invalidated keys rerun the loaders that read them

- **Given** a live page with a layout loader, a page loader that reads `sales` and views that read `issues`, `other` and `issue:7`, streamed with a reusable plan
- **When** `issue:7` is invalidated outside any command (a background service calling `IDataKeys.Invalidate`)
- **Then** only that view's loader runs again, in a new scope, its model receives the data, and one `views` event carries that view's region alone (the HTML of an `X-Aioros-View` answer)
- **When** `issues` is invalidated
- **Then** only the view that read it runs again
- **When** a key no loader read is invalidated
- **Then** no loader runs and nothing is sent, and the next event is the next view's that an invalidation reached
- **When** `sales` is invalidated
- **Then** the layout's and the page's loaders run again (they answer together) and the event carries the page's body.

*Precedent: Turbo 8 page refreshes (`broadcasts_refreshes`, `turbo_stream_from`), SvelteKit's `invalidate(key)` rerunning the loads that depend on it, LiveView's diff per component.*

### SSR-116: Each connection reruns its loaders with its own user

- **Given** a live page whose loader reads `sales` and returns the name of the request's user, streamed to Ada and to Grace
- **When** `sales` is invalidated
- **Then** each stream reruns the loader with its own user and sends its own name, never the other's.

*Precedent: Phoenix LiveView renders per connection; Turbo's broadcasts render outside any request, where `current_user` is undefined, which signal-then-rerun avoids.*

### SSR-117: The installed apps stream carries only its invalidated keys

- **Given** the installed app's stream `GET /_live?keys=sales,issue%3A7`
- **When** it opens
- **Then** it answers `text/event-stream` (.NET 10 `TypedResults.ServerSentEvents`) with an `open` event
- **When** `other`
- **Then** `sales`, then `issue:7`, then everything are invalidated, then it sends `keys` events carrying `sales`, then `issue%3A7`, then `issue%3A7,sales` (each key escaped as a URI component, sorted, comma separated, as `X-Aioros-Revalidate`), never a key it was not asked for and never data; a request without keys or with more than 256 answers 400.

*Precedent: React Router's `useRevalidator` on a socket message, htmx `hx-trigger="sse:…"`, Turbo 8's refresh signal.*

### SSR-192: An invalidation during the settle of a refresh is still pushed

- **Given** a live page whose loader reads `sales`, streamed with Avalonia's thread in the process
- **When** a key the page does not read is invalidated (the stream wakes, reruns no loader and waits for the UI thread to settle) and `sales` is invalidated while it waits
- **Then** the stream still reruns the loader and sends one event with the new data once the UI thread has run: a wake-up from an invalidation is never discarded as the refresh's own model change, so the invalidated keys never stay pending until a later invalidation.

*Precedent: LiveView and Turbo 8 refresh on every broadcast they receive; a model's change and a data key's invalidation are separate signals, as SvelteKit keeps `invalidate(key)` apart from a component's own state.*

## Upload

### SSR-139: Pages with uploads post as multipart

- **Given** a compiled page that takes uploads
- **When** its document is written
- **Then** its form is `<form method="post" enctype="multipart/form-data" ...>`, so a post without the script carries the files (Rails' `form_with` turns multipart on for a file field); a page without uploads keeps a form without `enctype`; the plans file keeps the mark.

### SSR-140: Multipart posts hand their files to the engine

- **Given** a page post sent as `multipart/form-data` with a text field and a file
- **When** it is handled
- **Then** the engine receives the text field among the form's values and the file as a posted file with its field name, file name, content type, length and readable content; a file part whose file name is empty (an input with no file chosen) is not passed.

### SSR-141: Files over the limit are refused

- **Given** `SsrOptions.UploadLimit` (`Aioros:UploadLimit`, 10 MB by default)
- **When** a multipart post carries a file larger than the limit
- **Then** it is answered 413 and the engine is not called, while a file at the limit is passed (Blazor's `OpenReadStream(maxAllowedSize)`, ASP.NET Core's `MultipartBodyLengthLimit` per section); a cross-site multipart post is answered 403 before its body is read.

## Upload script

### SSR-142: Chosen files are posted as multipart

- **Given** a page in the browser with a file input in a label and a button
- **When** a file is chosen
- **Then** the label shows the chosen file's name (`data-chosen`)
- **When** the button is pressed
- **Then** the framework's script posts the form as `multipart/form-data` carrying the file and the button, and shows the answer; a form without a file input is still posted URL-encoded.

### SSR-251: A chosen file name is written in its name text

- **Given** a page holding a file input in a label whose name text is marked `data-aioros-file-name` (a themed file picker, BUTTONS-117; another label before it naming the input with `for`, as a form's `Label` does) and a second file input in a plain label
- **When** the visitor chooses `photo.png` in the first and `a.pdf` and `b.pdf` in the second
- **Then** the first's name text reads `photo.png` and its label shows no `::after` copy, and the second's label shows `a.pdf, b.pdf` after its text as before (`data-chosen`), with no request.

*Precedent: the browser's own file input showing the chosen file's name beside its button.*

## Prompt script

### SSR-154: A prompt is shown modal and its buttons answer it

- **Given** a page in the browser whose post answer holds a `<dialog open class="prompt">`
- **When** the answer is applied
- **Then**
  - The framework's script shows it as a modal dialog (`:modal`, the page behind it inert), and a button inside it posts the form with the `_prompt` field; the next answer, without the dialog, removes it.
  - Without JavaScript, the open dialog shows in place and its buttons post the same form.

### SSR-155: Notices are shown as popovers above a prompt

- **Given** a page in the browser whose post answer holds `<div popover="manual" class="notice">` elements
- **When** the answer is applied
- **Then** the framework's script shows each one (`:popover-open`, in the top layer, above an open prompt); a button inside a notice that carries the step that showed it (`data-replay`) posts the form with that step in `_prompt`, and a button inside a notice without it hides the notice without posting.

### SSR-197: A prompts escape and mask post its dismissal

- **Given** a page in the browser showing a prompt with a mask (`.pm`)
- **When** Escape is pressed
- **Then** the dialog stays open and the script posts the form with `_dismiss=escape` and the prompt's `_prompt`; when the mask is clicked, it posts `_dismiss=outside`; a click on the dialog's surface posts nothing; the answer replaces the page, removing the dialog when the engine closed it.

### SSR-236: An answer loaded before the script shows its overlays when the script starts

- **Given** a page whose answer to a post holds a `<dialog open class="prompt">` and `<div popover="manual" class="notice">` elements
- **When** the button is clicked before the framework's script has run (the script still loading, so the form posts natively) and the answer arrives as a whole document
- **Then** once the script starts it shows the prompt as a modal dialog and each notice as an open popover, as when it applies a post answer (SSR-154, SSR-155); a page that holds neither is unchanged.

*Precedent: Hotwire Turbo and htmx process the elements of a page loaded by a full navigation as they process a fetched one (Turbo's `connect` on every page load).*

### SSR-238: A notices button carries its step without the script

- **Given** a page whose notice holds an action button and the hidden `_notice` field naming that button's token and the step that showed the notice, in a browser with JavaScript disabled
- **When** the notice's button is pressed
- **Then** the native post reaches the engine with `_prompt` set to that step and the button's own `_btn`, and without `_notice`; when a button of the page is pressed instead, the post carries no `_prompt`.

*Precedent: HTML submits only the pressed button's name and value, so the fields a press needs beyond it travel as hidden inputs, as ASP.NET Web Forms' `__EVENTTARGET` and `__EVENTARGUMENT` do.*

### SSR-239: A page button pressed while a prompt is open carries no steps without the script

- **Given** a page with an open prompt whose hidden `_answers` field names the prompt's button tokens and field tokens beside its `_prompt` field, in a browser with JavaScript disabled
- **When** a button of the page outside the prompt is pressed
- **Then** the native post reaches the engine without `_prompt` and without `_answers`, so it runs only that button's command on a fresh page; when the prompt's own button is pressed, the post carries `_prompt`.

*Precedent: HTML's `<dialog>` shown without `showModal` leaves the page interactive and is not part of a submission from outside it in intent, and Web Forms scopes a postback's arguments to the control that raised it.*

### SSR-240: A page button pressed while a library dialog is open carries no steps without the script

- **Given** a page with a library dialog written open (`<dialog open data-aioros-modal>`, its state input and a `Save` button) whose answer carries the waiting command's `_prompt` replay and an `_answers` field naming `Save`'s token, in a browser with JavaScript disabled
- **When** a button of the page outside the dialog is pressed
- **Then** the native post reaches the engine without `_prompt` and `_answers`; when `Save` is pressed, it carries `_prompt`.

*Precedent: as SSR-239.*

### SSR-242: A popover written open stays open across answers

- **Given** a page whose popover is written open (`data-aioros-open`) holding a submit button
- **When** the button is clicked twice, each click posting and each answer writing the popover open again
- **Then** the popover is open after each answer: the script, which hides every popover before it reopens those written open, forgets what it opened of those still open, so an answer that writes one open as the last one did opens it again, while one the visitor closed stays closed (SSR-198).

## Plan miss

### SSR-152: Pages their plan cannot write are rendered by the engine

- **Given** a page whose cached plan throws a `PlanMissException` for a request's models (data unlike any the compiled page held)
- **When** the page is requested as a document, as the navigation script's dynamics (`X-Aioros-Plan` naming the plan's fingerprint) or by a post whose form the plan describes
- **Then** the answer is `200` with the document the engine renders for those models only to answer them, without compiling a plan (HTML, also for the dynamics request), the post's fields are bound and its command run by the engine, and the cached plan stays and still answers the next request it can write without the engine, as Next.js answers a static page that meets a dynamic API by rendering it dynamically.

## Static file

### SSR-165: Static files win over parameterized pages

- **Given** a web root holding `ports/semi.png` and a page route `/ports/{app}` mapped after the static files
- **When** `/ports/semi.png` is requested
- **Then** the file is answered (`200`, `image/png`, its bytes) and the page is not rendered
- **When** `/ports/semi` or `/ports/v1.2` (a dotted value with no file behind it) is requested
- **Then**
  - The page answers `200`.
  - A parameterized page never shadows an existing file of the web root, as in Next.js, which checks `public` files before dynamic routes; the file test runs only for a path whose last segment holds a dot, as ASP.NET Core's `nonfile` constraint on `MapFallback`, so an ordinary page request costs no file system lookup.

## Layout post script

### SSR-172: An engine post answer keeps the layout

- **Given** a page inside a layout (the page region marked inside the layout's markup) whose field marked `data-change` is not described by the plan, so its posts go through the engine
- **When** a visitor changes the field in a browser
- **Then** the framework's script posts it with the layout's signature and shows the answer, the page alone inside the kept layout (`X-Aioros-Partial`), in place: the layout's navigation is still there, the page shows what the post changed, and the document was not reloaded.

*Precedent: Turbo Drive and React Router, which swap the outlet and keep the layout for a form's answer as for a navigation.*

### SSR-173: A planned post answer keeps the layout

- **Given** the page of SSR-172 with its field described by the plan, so its posts are bound through the plan
- **When** a visitor changes the field in a browser
- **Then** the answer (the page's dynamics, or the page alone inside its layout) is shown in place with the layout kept, the change shown and no reload.

### SSR-180: A page answer keeps the pages own elements

- **Given** a page inside a layout
- **When** a post is answered with the page alone inside the kept layout (`X-Aioros-Partial`)
- **Then** the script morphs the page region keeping its own markers (`<!--p-->`, `<!--/p-->`), so the page's elements both renders share are the same nodes as before (a mark set on the page's title before the post is still on it after), where they used to be replaced, losing their state and replaying their CSS animations.

## Slot answer

### SSR-167: A post that changed only its slot is answered with the region

- **Given** a reusable page whose plan writes a slot region (a slot renderer's range field and command button between the region's marks) beside content outside it
- **When** the document is answered
- **Then** its head carries `aioros-slots` with each region's hash of the body outside it, and a dynamics answer carries the same hashes in `X-Aioros-Slots`
- **When** the navigation script posts, with the hashes it holds, a change of the region's field or a press of its button, and the post changed nothing outside the region
- **Then** the answer is `text/html` marked `X-Aioros-Slot: 0` with the hashes of the new body, whose body is only the region's content, the same bytes as between the region's marks in the page rendered from the same models, and less than a tenth of the whole page's answer.

*Precedent: Turbo Frames (a form inside a frame replaces that frame alone); LiveView's diff of one component; HTTP's entity tags, which tell whether what a client holds is still current.*

### SSR-168: A post that reached beyond its slot is answered whole

- **Given** the page of SSR-167
- **When** the posted command also changed content outside the region, or the post carries stale hashes, or comes without the script, or its changed field is outside every region
- **Then** the whole page is answered (the document, or the dynamics with the new body's hashes for a client holding the template), never the region alone.

### SSR-196: A dynamics answer to a post names its regions with the nonce its hashes carry

- **Given** a page with slot regions whose script holds its plan
- **When** a post carries no slot hashes (a live `views` push dropped them, SSR-182) and is answered with the page's dynamics
- **Then** the regions' marks in the dynamics carry the same nonce as the hashes the answer sends (`X-Aioros-Slots: {nonce}:…`), so the script finds the regions by that nonce and answers the next post with its region instead of reloading the page, which lost what the posts had set.

*Precedent: the hydration markers a framework tracks itself (React's, Leptos'), which a render and its patches must name alike.*

## Slot script

### SSR-169: The script shows a slots region in place

- **Given** a page with a slot region in a browser
- **When** the visitor changes the region's range field
- **Then** the framework's script posts the page's slot hashes, shows the region's answer in place (an element outside the region keeps a mark set on it, the region shows the new value, and the region's own elements are the same nodes from its first answer, with marks set on them kept and the focused range field still focused, as the answer is morphed between the region's own markers `<!--s-->` and `<!--/s-->`, which the answer itself does not carry) and keeps the answer's hashes, so a second change is answered with the region again
- **When** the visitor presses a submit button that carries a field's name and `data-change` (a choice's option)
- **Then** the script posts the form with `_chg` naming that field and the button's value.

### SSR-182: The script drops its slot hashes when it patches the page without new ones

- **Given** a page with a slot region whose hashes the framework's script holds (`aioros-slots`)
- **When** the script changes the page from an answer that carries no hashes (a live push of the page's body, a live `views` event, or a view region answer to a post)
- **Then** it drops the hashes it holds, so the next post of the region's field carries no `X-Aioros-Slots` and is answered with the whole page, never with the region alone proven against a page the browser no longer shows.

### SSR-185: Content cannot imitate a slot region

- **Given** a page whose body holds, beside a slot region, markup from a raw value that imitates one (an element carrying `data-aioros-slot` before the region, and the region's comment marks `<!--s-->`/`<!--/s-->` after it)
- **When** the visitor changes the region's range field in a browser
- **Then** the post is answered with the framework's region alone (`X-Aioros-Slot: 0`) and the script shows it in the framework's region element, the imitation untouched and the document not reloaded: the server takes the regions from where its region nodes wrote them during the render (never by scanning the body), and the region's element carries the render's nonce (`data-aioros-slot="{nonce}"`, also the first part of `aioros-slots`, `{nonce}:{hashes}`), which the script finds the region by and content cannot know.

*Precedent: React's and Leptos' hydration markers, which the framework writes and tracks itself rather than finding in content; a CSP nonce, which marks the page's own scripts.*

## Theme script

### SSR-174: A theme link switches in place

- **Given** a page rendered in the light theme with a link to `/theme/dark`
- **When** a visitor clicks the link in a browser running the framework's script
- **Then** the script fetches the link as a navigation without the plan and layout signatures, so the whole document is answered in the new theme through the switch's redirect, and morphs it in place: the theme cookie is `dark`, the root's `color-scheme` is `dark`, the document was not reloaded, an element present in both renders is the same node (so a library's kept elements replay no enter animation), and the address stays the page's; hovering the link never fetches it ahead (a prefetch would switch the theme).

*Precedent: Turbo Drive, which follows a link's redirect inside its fetch and renders the final page in place; Turbo 8's morphing page refresh, which keeps the elements both renders share.*

### SSR-175: Without JavaScript a theme link reloads the page

- **Given** the page of SSR-174 in a browser with JavaScript disabled
- **When** the visitor clicks the link to `/theme/dark`
- **Then** the browser follows the switch's redirect natively and shows the page reloaded in the dark theme.

### SSR-177: The script fixes the system theme without a hint

- **Given** a visitor whose theme cookie is `system` in a browser whose color scheme is dark and that sends no color-scheme hint
- **When** a page rendered with the light plan loads
- **Then** the framework's script stores `theme-system=dark` (path `/`, a year, `SameSite=Lax`) and fetches the page once more as a navigation without the plan and layout signatures, which shows it with the dark plan in place, without reloading the document
- **When** the device's scheme then changes to light while the page is open
- **Then** the cookie becomes `theme-system=light` and the page is fetched once more and shown with the light plan.

### SSR-178: The script leaves a matching theme alone

- **Given** a visitor whose theme cookie is `system` and a page already rendered in the variant the device's color scheme asks for
- **When** it loads
- **Then** the script makes no extra request
- **When** a visitor who chose `light` on a dark device, when the page loads
- **Then** the script makes no extra request either and only keeps `theme-system` current (`dark`).

## Live query script

### SSR-179: A navigation answered with dynamics reopens the live stream with its query

- **Given** a live page whose loader reads a query parameter (`/inbox?app=a`)
- **When** the script navigates to the same page with another value (`/inbox?app=b`) and the answer is the page's dynamics (the plan unchanged)
- **Then** the live stream is opened again with the new query (`/inbox/live?app=b&plan=…`) and the old one closed, so the next push shows the region of `app=b`, not of `app=a`; a post answered with dynamics after a redirect to the same page with a new query does the same, and an answer that leaves the address as it was keeps the stream open.

## Stand in navigation script

### SSR-181: A navigation to a document without the form loads it whole

- **Given** a page in a browser running the framework's script and a link to a page whose answer is a document without the framework's form (a native-only page's stand-in)
- **When** the visitor clicks the link
- **Then** the script loads the link's address as a whole document: the address, the title and the content are the stand-in's, the old page's script state is gone, and no script error is raised (the title is never changed on a page the script then leaves as it was).

*Precedent: Turbo Drive, which reloads the whole page when an answer is not one it can render in place (a missing or different tracked `<head>`, `turbo-visit-control: reload`); React Router's `reloadDocument`.*

## Proxied request

### SSR-184: A proxys forwarded scheme keeps the theme cookie secure

- **Given** an app behind a TLS-terminating reverse proxy, which reaches it over plain HTTP with `X-Forwarded-Proto: https`
- **When** `/theme/dark` is requested through a proxy on the loopback address (the proxies the framework trusts by default, ASP.NET Core's `KnownProxies`)
- **Then** the request is handled as HTTPS (its scheme is `https`, from the forwarded headers the framework applies) and the theme cookie is marked `secure`
- **When** it comes from a proxy the app does not trust
- **Then** the scheme stays `http` but the cookie is still marked `secure`, as the forwarded scheme can only make a cookie stricter; a plain HTTP request without the header (a LAN address) still gets a cookie without `secure` (SSR-171).

*Precedent: ASP.NET Core's forwarded headers middleware (`UseForwardedHeaders`, trusting loopback proxies unless configured otherwise, and every proxy under its `ASPNETCORE_FORWARDEDHEADERS_ENABLED` switch).*

## Live value script

### SSR-186: An answer sets the live values of the inputs it changes

- **Given** a page with two range fields marked `data-change` and a text field, in a browser
- **When** the visitor moves the second range (the input becomes dirty, so its `value` property no longer follows its attribute) and the post's answer writes it
- **Then** moves the first range past what the server accepts while typing a draft in the text field, focused, and the answer writes the first range clamped and the second one moved by the server, then the script sets each input's live value from the answer (`value`, and `checked` and `selected` for boxes, radios and options), so the ranges show and post the server's values (`4` and `2`), while the focused text field that the post did not change keeps the draft; text fields are synced only from a post's answer, never from a live push or a navigation in place over unsaved typing they did not post.

*Precedent: morphdom's and Idiomorph's special handling of form elements (`value`, `checked`, `selected` set as properties, the focused input left alone unless told otherwise).*

## Stream cache script

### SSR-188: Streams close when the page goes into the back forward cache

- **Given** a live page in development (the framework's live stream and the development stream open) in Chromium with its back/forward cache on
- **When** the visitor loads another page as a whole document
- **Then** both streams of the page left are closed (the server sees each request end), so a page kept in the back/forward cache holds none of the browser's connections to the host (HTTP/1.1 allows six), and when the visitor goes back the live stream is opened again.

*Precedent: web.dev's back/forward cache guidance (close connections on `pagehide`, reopen on `pageshow` when `persisted`); Turbo's and Vite's dev clients, which close their sockets on `pagehide`.*

## Queued change script

### SSR-191: Changes made while a post is in flight are kept and posted once after its answer

- **Given** a page with a range field marked `data-change` at `20` whose post the server is still answering, in a browser
- **When** the visitor presses ArrowLeft five times in a row (the first press posts `19`, the four others change the input while that post is in flight)
- **Then** no change is lost and none overwritten: the answer to the first post leaves the input the visitor changed since that post was sent at its live value (`15`, not the server's `19`), and the script then sends one more post with the form's latest values naming that field in `_chg`, so the server ends at `15` after at most two posts and the input shows `15`; only one post is in flight at a time, the fields changed meanwhile are posted in the order they first changed, each naming its own field, and a field the visitor did not change again is still set from each answer.

*Precedent: LiveView, which ignores the server's value for an input changed since the event was pushed and sends the next change once the previous one is acknowledged (`phx-change` with its pending-ack refs).*

## Page style

### SSR-193: Keyed styles are written once in the head

- **Given** the page of RENDERING-28
- **When** it is answered as a document, a navigation answer, the page inside its kept layout, or dynamics for a script that holds its template
- **Then** the keyed rule sets are written once each in the document's head as `<style data-css data-aioros-style="{key}:{hash}">` (the hash naming the rule set's content; `</` written `<\/` so a rule set cannot close the element), or, for dynamics, in the answer's `s` member, and never in the body, so three charts sharing `sync-a` carry its rules once.

*Precedent: styled-components' server-side style collection (`ServerStyleSheet`) written into the head; Next.js, which hoists the styles a page's components need into its head.*

### SSR-195: A live stream sends a keyed style only in a version it has not sent

- **Given** the page of RENDERING-28 streamed live
- **When** its model changes three times, the second change contributing a new version of `sync-a`
- **Then** the first push carries every keyed rule set, the second only the new `sync-a`, and the third none (a body or `views` push preceded by the style elements, dynamics in their `s` member): each connection remembers the version of each key it last sent, as it remembers the page's style sheet.

*Precedent: LiveView, whose diffs carry only the parts the client does not hold; Turbo 8's morphing refreshes.*

### SSR-257: A live stream sends a keyed style again in the version it replaced

- **Given** the page of SSR-195 streamed live
- **When** its model changes so that `sync-a` goes from its first version to a second and back to the first (Aioros.Cosmo's streamed code block, whose lines' rule set changes with the lines shown)
- **Then** the push that brings the first version back carries it again: the browser's script holds one version of a key and replaced the first by the second, so a connection remembers the version of each key it last sent, not every version it has sent.

## Page style script

### SSR-194: A region answer carries a keyed style only when the page lacks it

- **Given** the page of RENDERING-28 in Chromium, whose charts also write a range field
- **When** the visitor moves one chart's range and the post is answered with that chart's region
- **Then** the script's post names the rule sets the page holds (`X-Aioros-Styles: {key}:{hash} …`), and the region answer carries a keyed rule set only when the page does not hold it in that version: moving the range so that the renderer contributes the same `sync-a` answers the region alone without it, while a move that changes `sync-a` (or contributes a new key) answers the region preceded by the new `<style data-aioros-style>`, which the script moves into the head in place of the one with the same key, so the rules apply and the region holds no style element.

*Precedent: Turbo Streams and LiveView, whose partial answers carry only what changed; styled-components' client registry, which inserts a rule set only when its hash is new.*

## Open popover script

### SSR-198: Marked popovers follow their open state

- **Given** a page in Chromium whose flyout popover carries `data-aioros-open` (a flyout open when the page was written, ENGINE-221)
- **When** the page loads
- **Then** the popover is open; when the visitor closes it and a post's answer keeps the attribute, it stays closed; when an answer drops the attribute and a later one brings it back, it is closed and then open again, as the bound open state changed on the server.

### SSR-268: A planned answer resets the open popovers

- **Given** a page whose post is answered from its plan with dynamics (JSON, the plan's fingerprint matching), while a popover the visitor opened in the browser is open and a manual one the page marks open (`data-aioros-open`) is shown
- **When** the answer is applied
- **Then** the visitor's popover is closed and the marked one open, as SSR-198 does for an answer in HTML.Cosmo's navigation menu, whose link closed its menu while the engine answered and stayed open once step A answered it from the plan.

## Delayed change script

### SSR-199: A text field with a delay is posted once the visitor pauses

- **Given** a page with a text field marked `data-change data-delay="300"` (a text box with `a:Web.Delay="300"` or a binding `Delay` of 300 ms, TEXT-41) in Chromium, its timers driven by Playwright's clock
- **When** the visitor types `a`, `b` and `c` 100 ms apart
- **Then** nothing is posted until 300 ms after the last keystroke, then one post with `_chg` naming the field and its value `abc`
- **When** the visitor types `d` while that post is in flight
- **Then** the answer leaves the focused field at `abcd`, and one more post is sent 300 ms after that keystroke; leaving the field posts nothing more.

*Precedent: LiveView's `phx-debounce`, which leaves an input the visitor is typing in alone and sends its value once the visitor pauses.*

## Flag script

### SSR-211: Flags switch in the browser and survive an answer

- **Given** a page holding a panel marked `data-aioros-flag="IsOpen" data-aioros-on` whose side's rules give it 240 px under the mark and 48 px without (as ENGINE-238 writes them), with a button `data-aioros-flips="IsOpen" aria-expanded="true"` inside it, and a second panel with the same flag off and its own button
- **When** the first button is clicked
- **Then** the flag module (loaded because the page holds a flipping button) removes the first panel's mark with no request, its side measures 48 px and its button says `aria-expanded="false"`, while the second panel keeps its state; clicked again, the side measures 240 px; and when a post's answer morphs the page with the panel marked, a panel switched off in the browser stays off.

*Precedent: shadcn/ui's `SidebarProvider` (`toggleSidebar` switching `data-state` on the client), the WAI-ARIA disclosure pattern.*

### SSR-227: Bound flags are posted when switched

- **Given** a page in Chromium holding a library panel marked `data-aioros-flag="IsOpen" data-aioros-on data-change` with its hidden check box checked and a flipping button
- **When** the visitor clicks the button
- **Then** the check box follows the mark (unchecked) and the change is posted (`_chg` naming the check box, which the post leaves out); a second click checks it and posts it again; a flagged panel without `data-change` posts nothing (SSR-211).

### SSR-228: Flags keep their state in their cookie

- **Given** a library panel whose flag names a cookie (`data-aioros-cookie="sidebar_state"`)
- **When** its flipping button switches it in the browser
- **Then** the flag module writes the cookie with the panel's new state, `sidebar_state=false` then `sidebar_state=true`, for the whole site (`path=/`) and seven days (`max-age=604800`), as shadcn/ui's sidebar writes it; a panel without the mark writes none.

### SSR-229: Flags open their sheet below their breakpoint

- **Given** a flagged panel with `data-aioros-breakpoint="600"` holding a `<dialog data-aioros-sheet>` with a mask that flips the panel, and its trigger outside the sheet
- **When** the viewport is 390 px wide and the trigger is clicked
- **Then**
  - The sheet opens as a modal dialog (`showModal`: the page behind it inert, the focus inside it) without switching the panel's mark, writing its cookie or posting, and the trigger says `aria-expanded="true"`
  - Escape closes it, and so does a click on the mask; when the viewport is wide, the trigger switches the panel's mark as before and the sheet stays closed; and a sheet open when the viewport widens past the breakpoint is closed.

### SSR-253: Expanders follow their flag

- **Given** a flagged panel, off, holding a `<details>` marked `data-aioros-follows="IsOpen"` and `data-aioros-open-on="true"` and a button flipping the flag
- **When** the visitor clicks the button and then the details' summary
- **Then** with no request the button opens the details along with the flag (`data-aioros-on`, `aria-expanded="true"`), and closing the details from its summary turns the flag off again (`aria-expanded="false"`).

*Precedent: Radix's controlled `Collapsible` (`open` and `onOpenChange`), the HTML `toggle` event of `<details>`.*

### SSR-256: An answer changing a flags value switches it

- **Given** a page in Chromium holding a panel marked `data-aioros-flag="IsOpen" data-aioros-cookie="sidebar_state"` served, with a flipping button, whose value each answer serves from the model
- **When** the visitor switches it off in the browser and a post's answer serves it off
- **Then** it stays off
- **When** the next answer serves it on (the server's value changing from the one it last served, as Aioros.Cosmo's sidebar-09 opens its sidebar when a folder is chosen)
- **Then** the panel takes the mark, its button says `aria-expanded="true"` and the cookie reads `true`
- **When** the visitor switches it off again and an answer serves it on once more (the value the server last served)
- **Then** it stays off, as SSR-211 keeps a flag switched in the browser.

*Precedent: the morph's `<details>` (an answer's `open` applied only when it differs from the one last served), shadcn/ui's sidebar (`setOpen` writing its cookie).*

### SSR-271: A flags cookie is secure in a secure context

- **Given** a library flag that keeps its state in a cookie (`data-aioros-cookie`) on a page in a secure context (HTTPS, or a loopback address as browsers treat it)
- **When** the visitor flips it
- **Then** the cookie the framework's script writes is `Secure` as well as `SameSite=Lax`, as the server's own cookies are over HTTPS (SSR-184); on a page that is not a secure context (plain HTTP from a LAN address) it is written without `Secure`, which the browser would drop there (SSR-171; the security audit's F6).

## Keyboard script

### SSR-212: Calendar arrows move the focus over the shown days

- **Given** a page holding a calendar marked `data-aioros-calendar` (as RANGES-17 writes it, a hidden week and a day `aria-hidden` among its days) in Chromium
- **When** the page loads
- **Then** the keyboard module (`/_aioros/keys/{hash}.js`) is loaded
- **When** the visitor focuses a day and presses the arrows
- **Then** ArrowRight and ArrowLeft move the focus to the next and previous day, ArrowDown and ArrowUp a week, Home and End to the ends of the week, skipping hidden and `aria-hidden` days and staying put at the edges, the focused day taking the roving tab stop (`tabindex="0"`), without posting.

*Precedent: the WAI-ARIA date picker dialog's grid keys; react-day-picker's `useFocus` (arrows by day and week, Home and End to the week's ends).*

### SSR-213: Control clicks on calendar days are posted

- **Given** the calendar of SSR-212
- **When** the visitor clicks a day with Ctrl (or Meta) held
- **Then** the calendar's hidden field ends in `c` as the post leaves; a click without modifiers leaves it ending in nothing.

### SSR-226: Enter in a date pickers field posts its change

- **Given** a page in Chromium holding a date picker's text field (`[data-aioros-picker] input[data-change]`) and, before it in the form, a submit button
- **When** the visitor types in the field and presses Enter
- **Then** the form posts the field as the change (`_chg`) and no button, rather than the browser's implicit submission pressing the form's first button; as Avalonia's picker parses its text on Enter.

*Precedent: HTML's implicit submission (the form's default button), which the keyboard module prevents for the field.*

## Culture

### SSR-214: The visitors request chooses the culture

- **Given** an app whose services configure ASP.NET Core's request localization (`AddRequestLocalization` with the supported cultures and UI cultures `en-US` and `fr-FR`, `en-US` by default)
- **When** a page is requested with `?culture=fr-FR`, with the `.AspNetCore.Culture` cookie `c=fr-FR|uic=fr-FR`, with `Accept-Language: fr-FR,fr;q=0.9`, with `Accept-Language: de-DE` and with nothing
- **Then** the engine renders it with `fr-FR` as the current culture and UI culture for the first three and `en-US` for the last two, the query before the cookie before the header.

*Precedent: ASP.NET Core's `RequestLocalizationMiddleware` and its default providers (query string, cookie, `Accept-Language`); Blazor Server's culture cookie.*

### SSR-215: Answers name their language and what chose it

- **Given** the app of SSR-214
- **When** its pages are answered
- **Then** each answer carries `Content-Language` and `<html lang>` naming the culture it was rendered in (`fr-FR`, `en-US`), and `Vary` names `Accept-Language` when the header or the default chose it, `Cookie` when the cookie did (which a page answer that may depend on the visitor names anyway, SSR-231) and not `Accept-Language` when the query did, beside the headers it varied by before.

*Precedent: ASP.NET Core's `ApplyCurrentCultureToResponseHeaders`; RFC 9110's `Content-Language` and `Vary`.*

### SSR-216: Apps that do not localize answer as before

- **Given** an app that configures no request localization
- **When** its pages are answered
- **Then** they render in the process's culture as before, `<html lang>` is `SsrOptions.Language` (`en`), and no answer carries `Content-Language` or varies by `Accept-Language`.

### SSR-217: Plans are kept per culture

- **Given** the app of SSR-214 with reusable plans
- **When** a page is requested in `fr-FR`
- **Then** in `en-US`, then in `fr-FR` again, then its plan is compiled once per culture and the third answer is written from the `fr-FR` plan without the engine.

*Precedent: the plans kept per theme (SSR-19); ASP.NET Core's `ResourceManager` cache per UI culture.*

### SSR-219: Warmup compiles every culture and theme

- **Given** the app of SSR-214 warming its pages at startup
- **When** the warmup runs
- **Then** each page is loaded and compiled for every supported culture and both themes (`en-US` and `fr-FR`, light and dark), each render under its culture, and the outcome names the culture of every entry.

### SSR-220: Precompiled plans are served per culture

- **Given** plans precompiled for `en-US` and `fr-FR` loaded at startup
- **When** a page is requested in each culture
- **Then** each answer is written from that culture's plan and the engine renders nothing.

### SSR-221: Posts and live refreshes run in the visitors culture

- **Given** the app of SSR-214
- **When** a visitor in `fr-FR` posts a page's form and when a live page of theirs refreshes
- **Then** the engine runs the post and the refresh renders under `fr-FR`.

### SSR-223: Weeks start on the day of the rendered culture

- **Given** a page with a themed calendar that does not set `FirstDayOfWeek` and one that sets it to Wednesday
- **When** the engine renders it from an `fr-FR` caller and then from an `en-US` caller
- **Then** the first calendar's weeks start on Monday in `fr-FR` and on Sunday in `en-US`, as each culture says, and the second's on Wednesday in both: Avalonia reads a calendar's default first day of the week once, from the culture of the first render, so a calendar the app and its theme leave unset is given the current culture's.

*Precedent: `DateTimeFormatInfo.FirstDayOfWeek`; react-day-picker's `weekStartsOn` taken from its `locale`.*

### SSR-224: Nothing of a culture is kept for the next

- **Given** a page with a date and a number bound with `StringFormat`, a themed calendar and a `CalendarDatePicker`
- **When** the engine renders it from an `en-US` caller
- **Then** from an `fr-FR` caller, then from an `en-US` caller again, then the two `en-US` renders are the same bytes and the `fr-FR` one is French (`jeudi 12 juin 2025`, `1 234,50`), so nothing of one culture is kept for the next.

## Culture setting

### SSR-222: The cultures setting localizes the website and not the desktop app

- **Given** an app that sets `Aioros:Cultures` to `en-US,fr-FR` in its configuration (`Aioros__Cultures` in the environment)
- **When** it is composed as a website
- **Then** its request localization supports those cultures with the first as the default, as SSR-214 picks them
- **When** it is composed as the desktop app
- **Then** no request localization is configured and its windows keep the operating system's culture.

## Scroll script

### SSR-230: Viewers follow their end and buttons scroll there

- **Given** a `data-aioros-follow` viewer at its end and a `data-aioros-scrolls` button naming it
- **When** the page loads
- **Then** the button is hidden (`data-aioros-at-end`)
- **When** the visitor scrolls the viewer up
- **Then** the button shows
- **When** the viewer's content grows meanwhile
- **Then** the viewer keeps the visitor's place
- **When** the button is clicked
- **Then** the viewer scrolls to its end with no request and the button hides
- **When** the content grows while the viewer is at its end
- **Then** it stays at its end.

*Precedent: `@shadcn/react` 0.3.1's `MessageScroller` (`following-bottom` and `free-scrolling`, the button active while the viewport is not at its end).*

### SSR-241: A following viewer returns to its end when its content grows

- **Given** a `data-aioros-follow` viewer at its end, within the 1 px the module counts as its end but not at the scroll origin (an offset of -1 px, as a smooth scroll can leave it)
- **When** its content grows and the browser's scroll anchoring keeps the old content in place (-61 px; SSR-230 hit it about once in forty runs)
- **Then** the module brings the viewer back to its end, so the newest content stays in view; and a viewer the visitor scrolled away from its end keeps its place when its content grows (no jump to the end).

*Precedent: `use-stick-to-bottom` (StackBlitz), which `@shadcn/react`'s `MessageScroller` follows, re-pinning the end in a `ResizeObserver` while the viewer was at its end.*

## Copy script

### SSR-232: Buttons copy their text in the browser

- **Given** a page holding a button with `data-aioros-copies="npm install"`
- **When** it is clicked
- **Then** with no post and no navigation the visitor's clipboard holds `npm install` and the button carries `data-copied`
- **When** it is clicked again 1.5 s later
- **Then** the mark stays until 2 s after that second click and then goes (the browser's clock driven by the test); and the copy module is loaded only by pages holding such a button.

## Choice script

### SSR-234: Lists choose in the browser

- **Given** a page holding a list marked `data-aioros-choice data-aioros-toggle` with its hidden field, its shown part (`data-aioros-shows`), a `<template data-aioros-value>` for each index and its options (`data-aioros-chooses`, submit buttons) in a popover
- **When** an option is clicked
- **Then**
  - With no request the shown part is the template's for its index, the option alone is `aria-checked="true"`, the popover closes and the field holds the index and is enabled; clicking the chosen option again shows the template of `-1` and writes `-1`
  - Enter in the list's filter chooses the selected option as a click does; with `data-change` on the field a choice posts the page at once as that field's change; after a page's answer changes a template's content, a choice shows the new content; and the choice module is loaded only by pages holding `data-aioros-chooses`.

*Precedent: cmdk's `onSelect` closing the Radix popover in shadcn/ui's combobox, and the native `<select>`, whose choice is a field of its form posted with it.*

## Details script

### SSR-235: Bound details are posted and follow their model

- **Given** a page with a `<details data-change>` holding its hidden check box, closed, and a plain `<details>`, closed
- **When** the visitor opens the first
- **Then** its check box is checked and posted as a change
- **When** the answer writes it closed again (its model's state changed back)
- **Then** the morph closes it and posts nothing more
- **When** the visitor opens the plain one and an answer writes it closed as before
- **Then** it stays open (the visitor's state kept)
- **When** an answer writes the first open while the previous answer wrote it closed
- **Then** it opens, as the model changed it.

*Precedent: Phoenix LiveView and React keep a client's state unless the server's value changes.*

## Dialog post

### SSR-237: A notice is shown without JavaScript

- **Given** a page whose answer to a native post holds a notice, in a browser with JavaScript disabled
- **When** the answer is shown
- **Then** the notice is displayed at its corner above the page (`@media (scripting:none)`), while with JavaScript a notice is still shown only by the script's `showPopover`, so the script's closing and expiry keep working.

*Precedent: Rails' and Phoenix's flash messages, rendered visible in the HTML of the answer to a form post; Media Queries Level 5's `scripting` feature (Chromium 120, Firefox 113, Safari 17).*

### ENGINE-309: A post the plan answers is not written unless it left a prompt

- **Given** a page the engine posts to with `planAnswers`
- **When** the post's command leaves no prompt open
- **Then** the engine returns its status with no page (it does not write the page a second time; the plan answers), and when the command opens a prompt, it writes the page with the prompt as before, since only its render shows the prompt.

## Form busy script

### SSR-243: A form stays busy until its queued changes are answered

- **Given** a form whose fields post when they change (`data-change`) and a server holding the answer to the first change
- **When** a second field changes while that post is in flight and the answer is then released
- **Then**
  - The form carries `aria-busy` from the first post until the answer to the queued second change is applied and loses it once, with both fields as the last answer wrote them; and a post that fails for the network leaves the form not busy.
  - `aria-busy` says the form still has work in flight, so a page's script, an assistive technology or a test can await it.

*Precedent: WAI-ARIA's `aria-busy` ("an element is being modified and assistive technologies may want to wait until the modifications are complete").*

## Deck script

### SSR-245: A deck expands under the pointer and stops its expiry

- **Given** a deck of three notice cards (`data-aioros-deck="14 3 0.05"`, anchored at the bottom, written collapsed, each expiring after a minute) on a page in Chromium
- **When** the pointer moves over the deck
- **Then** the cards stand 14 px apart at their own heights as the browser lays them out (the middle one's `data-h`, 40, written shorter than its 50 px, is not trusted), none `data-behind`, at scale 1, the deck as tall as the cards and their gaps, and their expiry stops (their animations paused); when the pointer leaves, they collapse back (the second 14 px up at scale 0.95 and as tall as the newest, `data-behind`) and their expiry runs again; a press on the deck also stops it until released; and a deck marked `data-aioros-expanded` stands expanded with its expiry stopped while the pointer is away.

*Precedent: sonner 2.0.7 (`expanded`, `interacting`, `isDocumentHidden` pausing its timers).*

### SSR-246: A deck takes out the cards that leave and closes up

- **Given** a deck of three notice cards in Chromium, the newest expiring in 50 ms and holding a close button (`data-aioros-closes`) and a submit button
- **When** the newest expires
- **Then** it is taken out once its exit animation has ended and the stack closes up (the next card at the front: `translate:0 0px`, scale 1, no `data-behind`, its own height); when the new front card's close button is clicked, it leaves at once with its exit, without a request, and is taken out; when the last card's submit button is clicked, the form posts and the card leaves; and when the deck has no card left it is closed and removed.

*Precedent: sonner's `deleteToast` on its close button and after an action.*

### SSR-247: A later answers cards join the deck in front

- **Given** a page whose deck shows a card from a first answer (its `data-replay` naming its step)
- **When** a second post's answer brings a deck with another card (and another replay)
- **Then** the first card stays the same element, still counting its expiry, and moves behind; the second joins in front, carrying its own replay, so its button posts its own step (`_prompt`); and an answer with no notices leaves the deck as it is.

*Precedent: sonner's toaster keeping its toasts across renders until each leaves.*

### SSR-248: A card swiped past its distance leaves the way it went

- **Given** a deck card marked `data-aioros-swipe="down right"` in Chromium
- **When** the mouse drags it 60 px right with a threshold of 45 px and a velocity it cannot reach
- **Then** it follows the pointer and, let go, leaves to the right (`data-out="right"`) and is taken out once its exit has ended; dragged 20 px right and let go, it moves back (no transform); dragged left, against its directions, it follows dampened (sonner's `1 / (1.5 + d / 20)`: 30 px of the pointer move it less than 30 px) and moves back; and with a velocity it always reaches, a 30 px drag lets it leave; a drag that started on a button inside the card does not move it.

*Precedent: sonner 2.0.7's `onPointerDown`, `onPointerMove` (`getDampening`) and `onPointerUp` (`SWIPE_THRESHOLD`, velocity over the drag).*

### SSR-250: A decks cards keep the sheet they came with

- **Given** a page whose deck shows a card from an answer that brought its own page sheet (`<style data-aioros-style="g:1">` holding the card's look)
- **When** a later answer brings no notice and a sheet of the same key (`g:2`)
- **Then** the card keeps its look (the first sheet stays in the head, marked `data-aioros-kept`, beside the new one, as a page's head sheets, `data-css`, stay while a card that came with them is in the deck); and once the card is closed and taken out, the kept sheet is removed and only `g:2` remains.

*Precedent: sonner's toasts keep their styles while they stay mounted across renders; Turbo keeps a permanent element's content across visits (`data-turbo-permanent`).*

## Swipe script

### SSR-249: A dialogs surface dragged down closes it

- **Given** an open marked dialog (`data-aioros-modal`) whose surface is marked `data-aioros-swipe="down"` with `data-aioros-swipe-by="0.25 1000"`, and its mask (`.pm`), in Chromium
- **When** the mouse drags the surface down by a fifth of its height
- **Then** the surface follows (its `translate`) with no transition and the mask fades to 0.8 opacity, and let go it moves back and the mask returns; dragged down by a third of its height and let go, the dialog closes from where the surface stands (its state input `False`); dragged up, the surface moves only by vaul's dampened distance (`8 · (ln(d + 1) − 2)`, none under 6.4 px) and moves back; opened again, it stands at rest; and with a velocity it always reaches, a short drag down closes it.

*Precedent: vaul 1.1.2's `onDrag` (`dampenValue`) and `onRelease` (`CLOSE_THRESHOLD`, `VELOCITY_THRESHOLD`).*

## Code slots script

### SSR-252: Code slots show the inputs characters and its caret

- **Given** a slot list marked `data-aioros-slots` of six slots (each with a `data-aioros-slot-text` and a `data-aioros-slot-box`) and its input marked `data-aioros-slots-input` beside it
- **When** the visitor focuses the input, types `12`
- **Then** `3456` and then leaves it, then on focus the first slot is active (`data-aioros-active`), after `12` the slots read `1`, `2` and the third is active, after `3456` they read `1` to `6` and the last is active, and once the input loses the focus no slot is active, with no request.

## Answer script

### SSR-254: A button waits for an answer in the browser

- **Given** a region marked `data-aioros-answers` holding two radio choices and a text input that are shown, a checked choice that is hidden, and a disabled button marked `data-aioros-needs-answer`
- **When** the visitor checks a choice, unchecks it and then types in the input
- **Then** with no request the button is enabled while a shown choice is checked, disabled again once none is, and enabled again while the input holds text; the hidden checked choice never counts.

*Precedent: shadcn/ui's `QuestionnaireNavigationState` example (`QuestionnaireNext` `disabled` while its item's status is `unanswered`), HTML's `checkVisibility`.*

## Key scope script

### SSR-255: A key scopes buttons answer their keys in the browser

- **Given** a region marked `data-aioros-keyscope` holding a radio choice, a check box, a text input, a hidden button and a shown one both listing `ctrl+Enter ArrowRight` in `data-aioros-keys`, a shown button listing `Enter`, an inner region with its own button listing `ctrl+Enter`, and a text input outside every region
- **When** the visitor presses keys
- **Then**
  - With no request: Ctrl+Enter in the text input clicks the shown button only, and inside the inner region its own button only
  - ArrowRight is ignored in the text input and on the radio choice and clicks the shown button from the check box
  - Enter on the unchecked check box and in the empty text input clicks nothing and leaves the page (the browser's implicit submission prevented), and on the check box checked clicks the `Enter` button, as from the text input once it holds text; and no key clicks anything from the input outside.

*Precedent: shadcn/ui's questionnaire (`@shadcn/react` 0.3.1's root `onKeyDown`: Ctrl or Meta and Enter moving, ArrowLeft and ArrowRight outside text fields and radio buttons, Enter from an answered answer), HTML's `checkVisibility` and radio groups' own arrow keys.*

## Engine answer CSS

### SSR-258: An engine answer keeps the plans CSS

- **Given** a page whose plan is cached (its CSS covering every state the plan writes)
- **When** a post the engine answers (one carrying a control's state, `_s0`) renders it again
- **Then** the answered document's stylesheet holds the plan's CSS and then each rule of the engine render's that the plan's does not hold, every rule once (a block of several lines, a container query, kept whole), so the browser, which takes the answer's stylesheet in place of the page's, keeps the rules the plan's later updates (slot answers, live updates) use; a post answered for a page with no cached plan carries the engine render's CSS alone.

## Stream

### SSR-259: A post naming its connection runs on that connections models

- **Given** a live page streamed to two connections, each with models of its own
- **When** a post naming the first connection (`X-Aioros-Live`, as the first stream's opening `event: connection` named it) presses a button whose command changes the page model's title
- **Then** the command runs on the first connection's model: the first stream pushes the new title, the second pushes nothing, and the answer carries `X-Aioros-Live-Kept`.

*Precedent: Phoenix LiveView's events reaching the socket's own process; Blazor Server's circuit.*

### SSR-260: A post naming no connection of its own loads its models anew

- **Given** the same live page streamed to one connection
- **When** a post names no connection, a name no stream holds, the connection after its stream ended, or names it from another page or for another user
- **Then** the post loads models of its own as before: its command leaves the connection's model as it was, the stream pushes nothing, and the answer carries no `X-Aioros-Live-Kept`.

### SSR-282: Posts naming one connection run one at a time

- **Given** the live page of SSR-259 streamed to one connection
- **When** two posts name that connection and the first one's command is still running when the second arrives
- **Then** the second post's command runs only once the first post has been answered, never beside it: the connection's models have one owner, its stream, which runs the posts naming it one at a time between its pushes; a post that reaches a connection whose stream has ended loads models of its own (SSR-260).

*Precedent: Phoenix LiveView, whose socket's process handles one event at a time from its mailbox; Blazor Server, whose circuit runs its events one at a time on its synchronization context.*

### SSR-283: A posts reloaded page becomes its connections

- **Given** a live page whose loader creates its model from its data (no `Receive`), streamed to one connection
- **When** a post naming that connection presses a page command, so the page's loader runs again and creates a new model
- **Then** the connection holds the post's page from then on: a later change of the new model is pushed by its stream, which no longer follows the model it opened with.

### SSR-290: A post on a connection follows the navigation its command asks

- **Given** the live page of SSR-259 streamed to one connection, its model holding the page's `INavigator` from the connection's scope
- **When** a post naming that connection presses a command that navigates (`NavigateAsync("?range=7d")`, as Aioros.Pictor's dashboard does when its brush moves)
- **Then** the post is answered with `303` to the address resolved against the post's page (`/inbox?range=7d`), as a post that loads its own models is, so the browser goes there and reopens the stream; a later post naming the connection whose command does not navigate is answered on the page, not sent to the earlier address again.

## Live state script

### SSR-261: Each tabs live page keeps its own state

- **Given** a live page open in two Chromium tabs
- **When** a button is pressed in the first
- **Then** the first tab shows the change its own stream pushes, its stream left open (no second `/live` request; the page is marked `data-aioros-live` once its stream named it), and the second tab still shows its own state, as each visitor's page keeps its own (React's component state).

## Planned post

### SSR-262: A posted state is restored through the plan before its fields

- **Given** a page whose cached plan records a state node at index 0 bound to the model's `State`, while the engine runs Avalonia in the process
- **When** a post carries `_s0` and a field
- **Then** the plan writes the posted state into the model through its path before it binds the field (the field's binding sees the restored state), answers `200` and the engine is not called.

*Precedent: ASP.NET Web Forms, which loads the view state before the posted data; Remix's multi-step forms, whose step is a hidden input bound like any field.*

### SSR-263: A state the plan does not record goes to the engine

- **Given** a page whose cached plan records no state node for a posted index (a travelling property set locally, an overlay's open state not bound), or records one the resolver cannot bind
- **When** a post carries that `_s{n}`
- **Then** the plan binds nothing and the engine answers the post, as before.

### SSR-265: A button whose command runs anywhere runs through the plan

- **Given** a page whose definition lists a button's command path among the commands that run on any thread (`AnywhereCommands`, from the generator), while the engine runs Avalonia in the process
- **When** a post presses that button
- **Then** the plan binds the post and runs the command without the engine; when the pressed button's command path is not listed, or the page's models use Avalonia (`ModelsUseAvalonia`), the engine runs the post as SSR-187 says.

### SSR-266: An undescribed button only matters when pressed

- **Given** a page whose cached plan records a button without a description (a menu item that toggles a bound flag the plan cannot follow)
- **When** a post presses another button or none
- **Then** the plan binds and answers it as if the undescribed button were not there; when a post presses the undescribed button, the engine answers it, never a `409`.

### SSR-267: A kept field leaves the post to the plan until it is posted

- **Given** a page whose cached plan records a kept field (a value input whose binding writes nothing)
- **When** a post does not carry it (the browser leaves out the fields of a closed dialog, which sit in a disabled fieldset)
- **Then** the plan binds and answers the post; when a post carries it, the engine answers the post, since the engine shows the posted value where the plan would show the model's.

### SSR-269: An unlisted command starts the engine

- **Given** a production app whose engine starts on demand and has not started (every page served from the plans file)
- **When** a post presses a button whose command the page does not list in `AnywhereCommands`
- **Then** the engine starts and runs the post (a command waiting on the visitor, as Aioros.Cosmo's dialog-open fixture's, answers instead of hanging); a press of a listed command runs through the plan and starts nothing; an engine that cannot run commands (no Avalonia in the process, a Native AOT build) leaves every press to the plan, as before.

### SSR-275: A posted state must be sealed by the server

- **Given** a page whose plan writes a library's travelling state (`_s0`) and an overlay's open state (`_m1`)
- **When** the web target answers it
- **Then** `_s0` is sealed with ASP.NET Core Data Protection (purpose `Aioros.ControlState`) in the document and in its dynamics, and `_m1` is not; when a post carries `_s0` sealed as written, the plan (and the engine) receive the value itself; when a post carries `_s0` forged, unsealed or sealed for another purpose (as every open page's state is once the keys that sealed it are lost: a restart with keys kept nowhere, another instance without the same keys), it binds and runs nothing and sends the page anew: the framework's script gets `409` with `X-Aioros-Redirect` naming the page's own address, a form posted without the script `303 See Other` to it; while `_m1` is taken as posted (the security audit's F4; decisions "Sealed control state" and "A state the server cannot open").

*Precedent: Phoenix LiveView reloading the page when its session token is stale; Turbo's `turbo-visit-control: reload`.*

### SSR-400: A post of a list item no longer there sends the page anew

- **Given** a planned page whose list's item is gone when a post from the page arrives
- **When** the post names that item's field and button
- **Then** the web target answers as for a state it cannot open (SSR-275): the framework's script gets `409` with `X-Aioros-Redirect` naming the page's own address, a form posted without the script `303 See Other` to it, nothing is bound and no command runs, so the script's reload keeps the visitor's draft and offers it back (SSR-280).

### SSR-401: A list without identity is a warning of the precompilation

- **Given** the warm-up of a page with a bound list whose items hold posted controls and have no identity
- **When** the precompilation reports
- **Then** the report lists the warning `AIOROS033` with the page's route and does not fail, and the warm-up logs it as `WarmupListWithoutIdentity`.

### SSR-431: A sealed state opens only under its own name

- **Given** a page whose travelling states are sealed (SSR-275), one at `_s1` and one per item of a keyed list at `_s0~{key}`
- **When** a post carries a state sealed for another item of the list, or for another name
- **Then** it is answered as a state the server cannot open (`409` with the page's address) and nothing is bound; a post carrying each state under its own name is answered as before.

*Precedent: ASP.NET Web Forms' ViewState MAC, which covers what it protects together with a `ViewStateUserKey`; ASP.NET Core Data Protection's purposes.*

### SSR-432: A field with an empty name is ignored

- **Given** a page answered from its plan
- **When** a post carries a field with an empty name (`=x`) or a malformed list mark, key or state name
- **Then** the post is answered as without that field or as a stale page, never as a server error.

### SSR-434: A stale post is never sent to another host

- **Given** a page reached at a path a browser reads as another host (`//evil.example/x`, through a catch-all route)
- **When** a post is answered as stale (`409` with `X-Aioros-Redirect`, or `303`)
- **Then** the address sent back is the site's root and not that path, as every other redirect of the framework already refuses a location that is not local.

*Precedent: ASP.NET Core's `IsLocalUrl`.*

## State

### SSR-264: A state that does not parse is left out by the plan

- **Given** a page whose cached plan records an `int` state node
- **When** a post carries text for it that does not parse (`_s0=many`)
- **Then** the plan leaves the model's value as it was, binds the post's fields and answers it, as the engine leaves out a state that does not parse.

## Engine capacity

### SSR-270: A full engine answers service unavailable

- **Given** a request whose page or post the engine refuses because its queue is full (`EngineBusyException`)
- **When** it is answered
- **Then** it answers `503 Service Unavailable` with `Retry-After: 1`, as ASP.NET Core's concurrency limiter answers a full queue, instead of a `500`.

## Content security

### SSR-273: HTML documents carry a content security policy

- **Given** an app outside development
- **When** it answers an HTML document (a page, a not found, the native-only stand-in)
- **Then** the answer carries `Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; media-src 'self' data: https:; font-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'`, which `SsrOptions.ContentSecurityPolicy` replaces and an empty value turns off; a policy the app set itself is kept, a non-HTML answer carries none, and development carries none (its reload channel is an inline script).

## Offline form script

### SSR-276: A change posted offline is posted again online

- **Given** a page whose field posts its changes (`data-change`)
- **When** its change is posted while the connection is down
- **Then** the change stays queued, the form is marked `data-aioros-sync="offline"`, and the change is posted again with the form's values at that moment (a change made meanwhile included) as soon as the browser is back online, or after a backoff of 1, 2, 4 seconds up to 30, the mark removed once it is answered: a change is a binding, posting it twice changes nothing more).

### SSR-277: A button posted offline is not posted again

- **Given** a page whose button posts a command
- **When** it is pressed while the connection is down
- **Then** the post is not sent again on its own and the form is marked `data-aioros-sync="unsaved"` until a later post is answered: a command whose request reached the server but whose answer was lost would otherwise run twice, as browsers never resend a post.

### SSR-278: Leaving with unanswered edits asks first

- **Given** a page's form
- **When** the visitor leaves it (closing or reloading the tab, or following a link the framework's script navigates) while a field's value differs from the one the server last answered, or a post is queued or unsaved
- **Then** the browser asks first (`beforeunload`, and a confirmation before a client navigation, which stays on the page when refused); a field typed back to its value, or a page whose fields all hold the server's values, leaves without asking).

### SSR-279: Edits kept in the browser are offered back

- **Given** a page with fields the visitor edits
- **When** the page is opened again before the edits are answered (a closed tab reopened, the address visited again; a reload whose fields the browser restores itself needs nothing)
- **Then** the framework's script, which kept them in the browser (IndexedDB, per page address), shows a notice (`.aioros-draft`, `role="status"`) offering to restore them or discard them, and applies them only when asked; a draft whose values the page already holds is not offered, and an answered post removes the page's draft; the document is marked `data-aioros-draft="saved"` or `"none"` as the page's draft is kept or removed).

### SSR-280: A stale page loads anew keeping its edits

- **Given** a page in Chromium whose form carries a travelling state the server cannot open (`_s0`) and a field the visitor has edited (kept as a draft, SSR-279)
- **When** a post's answer says the page is stale (`409` with `X-Aioros-Redirect`, SSR-275)
- **Then** the script loads the page anew without asking to leave (no `beforeunload` dialog), keeps the draft, and the page opened anew offers the edits back (`.aioros-draft`); the server bound and ran nothing.

*Precedent: Phoenix LiveView's reload on a stale session; Gmail and Google Docs keeping unsaved edits across a forced reload.*

### SSR-293: Fields whose values go nowhere are not the visitors edits

- **Given** a page in the browser with a field marked `data-aioros-local` (a radio group whose first radio carries the mark counts as marked), a text field without the mark and a link
- **When** the visitor changes only the marked fields and follows the link or leaves
- **Then** the browser asks nothing and no draft is kept for them; when the visitor edits the unmarked field, leaving asks first, as SSR-278 says.

### SSR-302: A post on its way is not an unsaved change

- **Given** a page in the browser with no field edited whose post is still waiting for its answer (a button with no command answered by the engine)
- **When** the visitor follows a link of the page
- **Then** the page is left without asking: a post on its way is not an unsaved change (it reached the server); a field edited and not yet posted, or a post kept for when the network is back, still asks (SSR-293, SSR-277).

### SSR-303: Links to their own page fragment are the browsers

- **Given** a link to a fragment of its own page (`href="#"`, `href="#pricing"`)
- **When** the visitor follows it
- **Then** the script lets the browser follow it (the address takes the fragment, the page scrolls to its target) and fetches nothing, asks nothing and leaves the page as it is.

*Precedent: Turbo, which does not visit same-page anchors; the browser's own fragment navigation.*

### SSR-315: A sent draft is not offered back on the same page

- **Given** a page in Chromium with a text field the visitor types into (kept as a draft, SSR-279) and a button whose answer clears the field (a chat's Send)
- **When** the visitor presses the button and the answer comes back
- **Then** no notice of unsaved changes (`.aioros-draft`) appears: the notice offers the edits of an earlier visit and is shown once, when an address is opened, never after an answer on the same page; the sent draft is removed (`data-aioros-draft="none"`) and the page opened again offers nothing.

### SSR-430: A password is never kept in a draft

- **Given** a page in Chromium with a password field and a text field the visitor edits
- **When** the edits are kept as a draft (SSR-279)
- **Then** the draft holds the text field and never the password, and a page opened again offers only what the draft holds.

*Precedent: browsers, which exclude password fields from form restoration; OWASP's guidance not to keep credentials in browser storage.*

## Code updates

### SSR-284: Code edits drop plans and notify the browser

- **Given** a running app in development
- **When** the runtime applies a C# edit (`dotnet watch` calls the `MetadataUpdateHandler`'s `UpdateApplication` with the updated types)
- **Then** `CodeUpdates` yields a `CodeReloaded` naming those types (none when the runtime names none), the cache drops every plan and the development channel sends a `reload` event, so the open tab refreshes in place with its state as it does after a XAML edit (SSR-09) and no manual reload is needed.

*Precedent: Blazor's and .NET MAUI's `MetadataUpdateHandler`, which re-render the components after a hot reload; React Fast Refresh, which re-renders on a code edit and keeps the state.*

## Development script

### SSR-285: A page refreshes in place when its development channel comes back

- **Given** a page open in development
- **When** its development channel ends (the app restarted, because `dotnet watch` restarts it for an edit hot reload cannot apply) and the browser reconnects to it
- **Then** the page refreshes in place with its state as after a `reload` event (SSR-09), so the edit shows without a manual reload; the channel starts with `retry: 250`, so the browser reconnects within a quarter of a second of the app being back rather than after its default of about three seconds; the first connection of a page does not refresh it.

*Precedent: Vite's client, which pings the dev server after its socket closes and reloads the page once the server answers again; Next.js, which reloads after `next dev` restarts.*

### SSR-286: Open streams end when the app stops

- **Given** a development channel and a live page's stream open
- **When** the app stops (`dotnet watch` restarting it, a deploy, `SIGTERM`)
- **Then**
  - Both streams end at once and the app exits without waiting for its shutdown timeout (30 s by default), so a restart for an edit hot reload cannot apply takes the time of the build and the start, and the browser reconnects to the new app (SSR-285).
  - Measured on the starter: an open development channel held a `SIGTERM` for 30,047 ms, against 51 ms without it.

*Precedent: the installed app's `/_live` stream, which ends on `ApplicationStopping`; SignalR, which closes its connections when the host stops.*

### SSR-288: Faults show over the page and clear once fixed

- **Given** a page open in development
- **When** a navigation, an in-place refresh or a post is answered with a fault (SSR-287)
- **Then** the page stays as it is and the fault is shown over it, with its type and message, `file:line`, the source lines with the failing one marked and the stack, instead of the browser leaving for the developer exception page (where no script reconnects, so the tab stayed on the error after the fix)
- **When** the code is fixed and the development channel sends `reload`
- **Then**
  - The fault is cleared and the page refreshes in place; a document answered with a fault shows it and reloads itself on the next `reload`.
  - A `build` event with errors (SSR-289) is shown the same way, `Build failed` with each error's code, message, `file:line:column` and source lines, until a `build` event with none clears it.

*Precedent: Next.js's dev overlay, which dismisses itself once the error is fixed and Fast Refresh applies the fix.*

## Development faults

### SSR-287: Page errors are answered with their fault

- **Given** the app in development and a page request that throws (its model, a loader, a command)
- **When** the error reaches ASP.NET Core's developer exception page
- **Then**
  - Aioros answers it: a request of the navigation script (`X-Aioros-Navigation: 1`) gets `500` with `X-Aioros-Fault: 1` and a JSON fault naming the innermost exception's type and message, the first stack frame in the app's own sources (a file under the content root, outside `obj` and `bin`) as its path relative to the content root, its line and column, the source lines around it (two before and two after) and the whole stack; a document request (`Accept: text/html`) gets a `500` HTML document showing the same fault with the development script, so it reloads itself once the code is fixed; any other request (an API's) gets the developer exception page as before; without an app frame the fault has no file; when the app's frames have no line (a method .NET Hot Reload replaced carries none), the fault names the first frame of the app's own code (a method of a type in the app's namespace, `IHostEnvironment.ApplicationName`) as `at Starter.Models.HomeModel.get_Message` instead.
  - `AddAiorosPages` registers it in development only.

*Precedent: Next.js's dev overlay (the error, the source frame with its code, the call stack); ASP.NET Core's `IDeveloperPageExceptionFilter`, the hook the developer exception page calls.*

## Development build

### SSR-289: The builds errors reach the page

- **Given** the app in development started by `aioros dev` with a development token (`Aioros:DevelopmentToken`)
- **When** the tool posts the build's errors to `/_aioros/dev/build` from the loopback address with that token
- **Then** the development channel sends a `build` event naming each error's file relative to the content root, its line, column, code and message, with the source lines around it (SSR-288 shows them)
- **When** it posts an empty list
- **Then** the channel sends a `build` event with no errors; a post without the token, with another one, from another address, or when the app has no token, is refused with `403` and nothing is sent.

*Precedent: Next.js's and Vite's overlays showing a compile error with its code frame and clearing once it is fixed.*

## Local field

### SSR-292: Fields whose values go nowhere are marked local

- **Given** a page with fields whose values go nowhere, as a showcase's are (a text box, a check box, a toggle switch and a radio group not bound to write back), and fields that do (a text box bound two way, a check box bound two way, and a text box named `x:Name` that another control may read by `#name`)
- **When** the page is written
- **Then** each of the first carries `data-aioros-local` right after its `name`, and none of the second does.

## Data table

### SSR-299: A planned post binds the changed field after the others

- **Given** Aioros.Cosmo's data table with its second row selected, answered from its plan
- **When** a post names the header's check box in `_chg`, checked, with the rows' check boxes as the page wrote them (the second checked, the others absent)
- **Then** the plan binds the changed field after the others, as the engine does (ITEMS-117): every row is selected
- **When** a post names the fourth row's check box, checked
- **Then** the second and fourth rows are selected and no other.

## Plan parity

### SSR-300: Every field of every input kind agrees with the engine

- **Given** a page with a field of each input kind (a text box, a check box, a select, a radio group bound to an enum through a converter, a check box bound to an enum through a converter, and a check menu item bound through a list's index) in a precompiled view
- **When** its plan is verified against the engine (decision "Deterministic plan coverage")
- **Then** every sample of every field agrees: posted as the browser posts it, the whole form with that field changed and named in `_chg`, through the plan into one load of the models and through the engine into another, both leave the same page, compared through the plan's render.

### SSR-301: A field the plan binds otherwise is named

- **Given** the same page and a field resolver whose binding of the text box writes nothing
- **When** the plan is verified against the engine
- **Then** the text box's samples are named as not agreeing, with where the two pages differ, and every other field's agree.

### SSR-304: A field that diverges is named and left to the engine

- **Given** the page of SSR-300 with a field resolver whose binding of the text box writes nothing
- **When** the app warms up with its posts verified (`aioros build`'s warm-up; the host that serves the app does not verify, since a verification's posts would change models or services the app shares)
- **Then** the warm-up's report names the text box (`WarmupFieldDiverges`, with the page's route and the field) and leaves it to the engine: a post that changes the text box (named in `_chg`) is answered by the engine, whose model takes the posted text, while a post that changes the check box is still answered from the plan; a page whose loads give the same model instance (one the app shares) is not verified.

### SSR-306: A page that renders differently on each load is not verified

- **Given** the page of SSR-300 whose model's title differs on every load (live data, a clock)
- **When** its posts are verified
- **Then** it is not verified: two loads with no post render differently, so the result says so (`Volatile`) and holds no verdict, and `aioros build`'s report names it (`posts not verified: its render changes from one load to the next`) instead of leaving its fields to the engine.

### SSR-307: A post the plan leaves to the engine says why

- **Given** the page of SSR-300 and a field resolver that cannot bind the text box (no binding for its kind)
- **When** its posts are verified
- **Then** each sample the plan leaves to the engine says why, as `PlanForms.Refusal` tells it without binding anything: `the plan does not bind it: fields it cannot bind: f0 (text)`; the reasons it tells are travelling states the plan cannot restore, fields it holds no description of, fields it cannot bind and kept fields the post carries, whose values only the engine shows.

### SSR-309: A posts local inputs show their posted values

- **Given** a page whose plan holds local inputs (ENGINE-310)
- **When** a post changing them is answered
- **Then** the page in the answer shows the posted values: through the plan when it binds the post, and when the engine answers a post the plan renders (`planAnswers`), its local values taken from the post as the engine's controls took them; the next request with no post renders the values the trace saw.

### SSR-310: A post carrying a field the plan cannot show is rendered by the engine

- **Given** a page whose plan holds an input it has no description of (a three-state check box a style of the page moves when checked, ENGINE-320) beside a text box bound two way
- **When** a post changing both is answered
- **Then** the engine answers it with its own render, in which the check box is checked and the model takes the text: a post carrying a field the plan cannot show (one it holds no description of, or a kept field) is never rendered through the plan (`planAnswers`).

## Engine plan answer

### SSR-305: The engine runs the press and the plan answers

- **Given** a page with a reusable plan whose post presses a button only the engine runs (Avalonia runs in the process and the command is not listed as running anywhere)
- **When** the post is answered
- **Then**
  - The engine binds and runs it without writing its answer (`planAnswers`), and the answer is the plan's render of the models the command left (the pressed title); when the page has no cached plan yet, the engine writes the answer as before.
  - A post carrying a travelling state the plan has no node for, a file, or one that left a prompt open keeps the engine's answer (ENGINE-309).

## Precompile report

### SSR-308: Fields a disabled fieldset holds are not posted

- **Given** a page whose closed dialog holds fields inside its disabled fieldset (as the modal script disables it), nested fieldsets included
- **When** its posts are verified
- **Then** those fields are not part of the form the verification posts, as the browser's form model leaves out every field a disabled fieldset holds; the fields outside it are.

## Local proof

### SSR-311: An input outside the guarantee no verification proved is answered by the engine

- **Given** a page whose plan holds an input outside the guarantee (a text box a style of the page bolds by its text, `TextBox[Text=x]` setting its `FontWeight`, a selector by a value no state stands for, which the plan does not follow) that no verification has proven
- **When** a post typing `x` in it is answered
- **Then** the engine answers it with its own render (the text box's weight as Avalonia gives it), never the plan, not even to render the engine's answer: an input outside the guarantee is the page's own state on the web only once its domain was walked.

### SSR-312: Verification proves the inputs outside the guarantee whose domains agree

- **Given** a page with two inputs outside the guarantee, a toggle button of the page's own control theme whose checked look the theme sets (`^:checked /template/ Border`) and the text box of SSR-311, and a page holding only that toggle button
- **When** both pages are verified as `--precompile` verifies them
- **Then** each sampled value of each input's domain (the values `LocalDomains` walks, ENGINE-322) is posted through the plan and through the engine in the light and the dark theme: on the first page the toggle button, whose every value agrees, is proven and the text box, whose value `x` disagrees, is not, the report naming it with the value, the theme and where the two renders part; the second page's toggle button is proven, recorded in the plans file under its page (`proven`) and read back from it, and a post checking it is answered by the plan, without the engine.

### SSR-313: A value the plan never learned fails its field not the page

- **Given** a page whose plan learned the values it shows (a path icon whose data a converter gives from a text box's two-way bound text, learned for the text the page was compiled with)
- **When** the page is verified as `--precompile` verifies it and a sample posts a text the plan never learned
- **Then** the verification does not fail the page: the field's verdict does not agree and says the plan does not hold the value, the engine answers that field's posts, and the page is precompiled.

### SSR-314: Locals are not bound for models the plan cannot write

- **Given** a page whose plan learned the values it shows and holds a local input
- **When** the engine has answered a post the plan was to render (`planAnswers`) and its command left a value the plan never learned in the models
- **Then** the plan's local inputs are not bound (`PlanForms.BindLocals` writes nothing and says so, since the plan cannot describe the page for those models), no error is raised, and the answer is the engine's render of the models with the plan's stylesheet joined before its own, as any answer the engine renders is (the browser keeps the plan's stylesheet; a render the plan cannot write lost the classes only the engine's holds, a code block its preserved whitespace).

## Model

### SSR-316: An expander posts its own toggle only

- **Given** a page in Chromium with a two-way `Expander` (`<details data-change>` and its hidden check box) holding a plain text field with no `data-change` of its own and a check box marked `data-change`, and a button after it
- **When** the visitor types in the field and clicks the button
- **Then** one post is sent, naming the button (`_btn`) and no `_chg`: the field's `change` on blur posts nothing, as a plain text box posts nothing on change (TEXT-41)
- **When** the visitor ticks the check box
- **Then** its own change is posted (`_chg` names it)
- **When** the visitor closes the expander
- **Then**
  - The expander's own check box is posted (`_chg` names it).
  - A `<details>` marked `data-change` speaks for its own toggle, unlike a list's box, which speaks for the fields inside it (ITEMS-116).

*Precedent: Radix's controlled `Collapsible`, whose `onOpenChange` fires only for its own toggle.*

### SSR-317: A press during a change post is sent after it

- **Given** a page in Chromium with a field marked `data-change` and a button
- **When** the visitor changes the field and presses the button while the change's post is still waiting for its answer
- **Then** the press is kept, not ignored: no second request is sent until that answer has been applied, and then one post is sent that names the button (`_btn`) and no `_chg`
- **When** the change's answer no longer writes that button
- **Then**
  - The press is dropped and nothing is posted for it.
  - A press during a post that was itself a press stays ignored (SSR-298).

*Precedent: htmx's `hx-sync="queue"` and LiveView's channel, which keep a form's events in order rather than dropping them.*

### SSR-318: Text typed during a change post is kept

Given a page in Chromium with a field marked `data-change`, a text field the server writes with a value and a second text field, when the visitor changes the first field and, while its post is still waiting for its answer, types in the text field, then, whether the visitor stays in it or moves on to the second, the answer that lands leaves the typed text and the focus where the visitor left them, as the page's other content takes the answer; a text field the visitor did not touch during that post takes the answer's value.

*Precedent: LiveView, which never overwrites a focused input and tracks `phx-feedback` for fields used since the render; React's controlled inputs in a transition, which keep what was typed during it.*

### SSR-433: A press held during a change is dropped when the list changed

- **Given** a page in Chromium with a field marked `data-change`, a list whose shape mark (`_shape`) the answer changes, and a button of the list with no key of its own
- **When** the visitor presses the button while the change's post waits (SSR-317) and the answer changes the mark
- **Then** the press is dropped and nothing is posted for it, as the button may now stand for another item; when the mark does not change, the press is sent as SSR-317 says.

### SSR-435: A draft is offered only to the user who wrote it

- **Given** a page in Chromium whose visitor is signed in (`HttpContext.User`)
- **When** the visitor edits a field (a draft, SSR-279)
- **Then** the draft is kept under a scope the server writes in the `aioros-draft-scope` cookie, sealed with ASP.NET Core Data Protection, so it names nobody; the same user is offered the draft again; when another user signs in on that browser the cookie is replaced and that user is not offered it; when no one is signed in the cookie is removed and the draft is not offered.

*Precedent: Next.js and Remix sessions keyed per user; ASP.NET Core's claims-bound antiforgery tokens.*

### SSR-436: A draft without a scope stays with the visitors not signed in

- **Given** a page in Chromium whose visitor is not signed in
- **When** the visitor edits a field
- **Then** the draft is kept per browser as SSR-279 says and offered to a visitor not signed in; when a user signs in on that browser, that older draft, which has no scope, is not offered to them.
