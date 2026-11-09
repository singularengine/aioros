# Desktop

The desktop target (`src/Targets/Aioros.Targets.Desktop`): the same pages and view models as the web, in a native Avalonia window. Routes become a navigation stack. Tests drive the navigator in a headless Avalonia window.

80 criteria. Each has an identifier and exactly one test.

## Desktop navigator

### DESKTOP-01: Paths show their page inside the layout

- **Given** the app's pages
- **When** the navigator goes to a path
- **Then** the window shows the matching page inside its layouts, with every model created and loaded with the route values and the query string (unescaped, as on the web), and the window title is the page model's title.

### DESKTOP-02: Links and hyperlinks navigate

- **Given** a page with an element that has `a:Web.Href` and a `HyperlinkButton` with a relative `NavigateUri`
- **When** either is clicked
- **Then** the navigator goes to that path.

### DESKTOP-03: Back returns to the previous page

- **Given** two pages visited in order
- **When** the navigator goes back
- **Then** the first page is shown again with its models loaded anew.

### DESKTOP-04: Commands redirect after running

- **Given** a page whose command sets `RedirectTo`
- **When** its button is clicked and the command has run
- **Then** the navigator goes to that path.

### DESKTOP-05: Theme paths switch the theme in place

- **Given** the path `/theme/toggle` or `/theme/dark`
- **When** the navigator goes to it
- **Then** the application theme changes and the current page stays.

### DESKTOP-06: Unknown paths show the not found page

- **Given** a path that matches no route, or a page model that reports not found
- **When** the navigator goes to it
- **Then** the not-found page is shown, or an empty page when the app has none.

### DESKTOP-07: Loaders redirect

- **Given** a page model that sets `RedirectTo` while loading
- **When** the navigator goes to the page
- **Then** it goes to that path instead.

### DESKTOP-08: Links to the page shown are current

- **Given** links (`a:Web.Href`) in a layout
- **When** the app navigates
- **Then** each link to the page shown (same rule as the web: its path or a prefix up to `/`) has the `:current` pseudo-class and the others do not, so the same `:current` styles mark the active link on the desktop.

### DESKTOP-10: Each page has its own scope

- **Given** page models that take scoped services (server functions, a `DbContext`) from a container that validates scopes, as ASP.NET Core does in development
- **When** the navigator shows a page and then another
- **Then** each page's models come from a scope of their own, as each web request does, and leaving a page disposes its scope.

### DESKTOP-09: Failed pages show the error page

- **Given** a link to a page whose model throws while loading
- **When** it is clicked
- **Then** the window shows the app's `Error` page inside its layouts (as the website answers 500 with it) and the navigator raises `Failed` with the exception, so the click never fails silently; without an `Error` page the built-in error view is shown (DESKTOP-35).

### DESKTOP-12: Shared layouts stay mounted

- **Given** two pages that share their outer layouts
- **When** the navigator goes from one to the other
- **Then**
  - The shared layouts stay mounted (the same controls, so their own state such as scroll and expanders is kept) and show their models loaded anew for the new request, only what lies below the innermost shared outlet is replaced, and the page left is torn down without writing into its models; a layout the new page does not have is replaced with it.
  - React Router keeps the matching parent routes mounted and reruns their loaders
  - Next.js keeps shared layouts mounted.

### DESKTOP-13: Commands navigate through the injected navigator

- **Given** a page whose command asks the injected `INavigator` to go to a path, with models taken from their own scope of a container that validates scopes (DESKTOP-10)
- **When** its button is clicked
- **Then** the window's navigator goes there as soon as the command asks, a relative path resolved against the current page as on the web (SSR-35), and the navigator's path wins over a `RedirectTo` the command also sets (SSR-36).

*Precedent: Prism `INavigationService` and Uno.Extensions `INavigator` navigate when called.*

### DESKTOP-14: Navigations while loading go elsewhere

- **Given** a page model that asks `INavigator` to navigate while it is created or loaded
- **When** the navigator goes to the page
- **Then** it goes to that path instead, as with `RedirectTo` (DESKTOP-07).

### DESKTOP-15: Other sites open outside the window

- **Given** a command that navigates with `INavigator` to an absolute `http` or `https` URL
- **When** it runs
- **Then** the URL is handed to the system browser through the window's launcher (Avalonia's `ILauncher`, as a `HyperlinkButton` with an absolute `NavigateUri` does) and the current page stays; a URL with another scheme is refused with `ArgumentException`, as on the web (SSR-37).

### DESKTOP-16: Asynchronous commands are awaited before their redirect

- **Given** a page whose button runs an asynchronous command (any shape of ENGINE-40 to ENGINE-42) that sets `RedirectTo` when it ends
- **When** the button is clicked
- **Then** the navigator runs the command once, awaits it and then goes to that path
- **When** a command that throws or faults
- **Then** the navigator raises `Failed` with the exception and shows the app's `Error` page, as for a failed navigation (DESKTOP-09).

### DESKTOP-36: Menu item commands are awaited before their redirect

- **Given** a page with a `MenuItem` (in a `Menu`, a submenu or an open `ContextMenu`) bound to an asynchronous command (any shape of ENGINE-40 to ENGINE-42) that sets `RedirectTo` when it ends
- **When** the item is clicked
- **Then** the navigator runs the command once, awaits it and then goes to that path, as for a button (DESKTOP-16); a command that throws raises `Failed` and shows the error page
- **When** the item's `DataContext` changes
- **Then** the command its binding brings is run the same way and `CommandSettled` is raised once it has ended.

### DESKTOP-93: The system theme path follows the operating system

- **Given** the path `/theme/system`
- **When** the navigator goes to it
- **Then** the application's requested theme variant becomes `ThemeVariant.Default`, so it follows the operating system's, and the current page stays.

*Precedent: Avalonia's `Application.RequestedThemeVariant`, whose `Default` follows the platform's setting.*

### DESKTOP-97: Linked runs navigate

- **Given** a page whose `TextBlock` has a `Run` with `a:Web.Href="/items/7"` after a plain `Run`
- **When** the pointer rests on the linked run
- **Then** the text shows the hand cursor, and on the plain run it shows none
- **When** the plain run is clicked
- **Then** the navigator stays
- **When** the linked run is clicked
- **Then**
  - The navigator goes to `/items/7`.
  - The run under the pointer is found as Avalonia's `SelectableTextBlock` finds a character (`TextLayout.HitTestPoint` inside the padding), each inline holding its text's length (a line break and an inline container one).

## Desktop loader

### DESKTOP-17: Loaders feed the view models and kept layouts

- **Given** a page and its layout with loaders
- **When** the navigator goes to the page
- **Then** each loader receives the route values and the query, and the window shows the layout's and the page's view models created with their loaders' data
- **When** it then goes to another page in the same layout
- **Then** the layout's loader runs again and the layout stays mounted (the same control, DESKTOP-12) with the fresh data: pushed into its view model when the view model takes new data and the layout's route values are the same, otherwise a view model created from it as its `DataContext` (DESKTOP-25).

### DESKTOP-18: Loaders that fail or navigate are followed

- **Given** a page whose loader throws
- **When** it is clicked
- **Then** the navigator raises `Failed` with the loader's exception and shows the app's `Error` page (DESKTOP-09)
- **When** a page whose loader asks `INavigator` to navigate, when the navigator goes to it
- **Then** it goes to that path instead and the page's view model is never created (DESKTOP-14).

### DESKTOP-19: Loader results show the not found page or redirect

- **Given** a page whose loader returns `LoaderResult.NotFound`
- **When** the navigator goes to it
- **Then** the not-found page is shown (DESKTOP-06) and the page's view model is never created
- **When** a page whose loader returns `LoaderResult.Redirect(path)`
- **Then** the navigator goes to that path instead, resolved against the page as on the web (SSR-45), and the page's view model is never created.

### DESKTOP-20: Loader titles name the window

- **Given** a page whose loader returns a title
- **When** the navigator goes to it
- **Then** the window's title is the loader's title, merged with its layouts' as on the web (SEO-09).

### DESKTOP-21: Each loader has its own scope

- **Given** a page and its layout whose loaders each take the same scoped service, one that refuses a second operation while one is in progress
- **When** the navigator goes to the page and both loaders are inside that service at once
- **Then** each loader has its own instance, in a DI scope of its own created from the app's root `IServiceScopeFactory`, and each scope is disposed (with `DisposeAsync`) as soon as its loader completes, also when it throws (SSR-49, SSR-50).

### DESKTOP-22: View models keep the page scope

- **Given** a page whose loader and view model both take a scoped service
- **When** the navigator goes to the page
- **Then** the view model gets the page scope's instance (DESKTOP-10), which stays alive while the page is shown, and the loader's instance is another one, already disposed; a loader's `INavigator` is still the page's (DESKTOP-18).

### DESKTOP-44: Static titles and templates name the window

- **Given** a page with a static `a:Page.Title` below a layout with `a:Page.TitleTemplate`
- **When** the navigator goes to it
- **Then** the window's title is the template applied to the page's title (SEO-11)
- **When** a page whose loader returns a title over its static one
- **Then** the window's title is the loader's, templated as well.

## Desktop revalidation

### DESKTOP-23: Successful commands revalidate the shown page

- **Given** a page and its layout with loaders
- **When** a button's command completes without an exception and no model reports `INotifyDataErrorInfo.HasErrors`
- **Then** both loaders run again, each in a new DI scope, the navigator raises `CommandSettled` with `true`, a view model that takes new data (`Receive(TData)`, GENERATOR-23) is kept and receives it, so a property its command set survives, and a view model that does not is created again from the fresh data and becomes the `DataContext` of the same control, which stays mounted.

*Precedent: React Router revalidates the active loaders after an action and keeps the route components mounted; SvelteKit updates `data` in place.*

### DESKTOP-24: Failed or navigating commands revalidate nothing

- **Given** a page with a loader
- **When** its button's command throws
- **Then** no loader runs again (the navigator raises `Failed`, DESKTOP-16)
- **When** the command completes but a model reports `HasErrors`
- **Then** no loader runs again, `CommandSettled` is raised with `false` and the view model with its errors stays
- **When** the command navigates with `INavigator`
- **Then** the next page loads and the page left is not revalidated.

### DESKTOP-25: Kept layouts take new data unless their route values change

- **Given** two pages in a layout with a loader whose view model takes new data
- **When** the navigator goes from one to the other and the layout's own route values (the parameters of its folder) are the same
- **Then** the layout's view model is kept and receives the fresh data
- **When** they differ (`/projects/1/...` to `/projects/2/...`)
- **Then** a new view model is created from the fresh data, as the route is another one.

*Precedent: Prism `IsNavigationTarget`, SvelteKit `{#key}` and TanStack Router `remountDeps` recreate the component when the key changes.*

### DESKTOP-26: The newest revalidation or navigation wins

- **Given** a revalidation whose loader is still running
- **When** a newer revalidation starts
- **Then** the older one's cancellation token is cancelled and, when its loader completes later, its data is discarded (`CommandSettled` with `false`): the models show the newer data
- **When** a navigation starts while a revalidation or an older navigation is still loading
- **Then** the older one is cancelled the same way and the newer page is shown.

*Precedent: React Router aborts stale loads (`AbortSignal`) and keeps the newest revalidation; Next.js and TanStack Router cancel a superseded navigation.*

## Desktop remote data

### DESKTOP-27: Remote navigations load through the data endpoint

- **Given** the desktop navigator with the app's server set (the remote loader results, DESKTOP-30) and that server running the same pages
- **When** it goes to a page with a layout loader and a page loader
- **Then** it requests `GET /_data{route}?{query}` once with the page's contract hash, runs no loader in process, creates the view models through the generated constructors from the data it read (GENERATOR-27) and names the window from the title; a `404` from the server shows the not-found page (from the same answer, DESKTOP-34) and a redirect is followed.

*Precedent: Remix single fetch and SvelteKit's client fetch `.data` and `__data.json` on navigation instead of running server loaders.*

### DESKTOP-28: Remote revalidations load through the data endpoint

- **Given** the same remote navigator on a page whose view model takes new data
- **When** a button's command succeeds
- **Then** the page's loader results are requested from `/_data` again and the kept view model receives the server's fresh data through `Receive` (DESKTOP-23).

### DESKTOP-29: Refused or outdated data requests fail the navigation

- **Given** the same remote navigator
- **When** the server answers `401` or `403` (a protected page without a signed-in user) or `426` (another data contract)
- **Then** the navigation fails through `Failed` with an `UnauthorizedAccessException` (for `401`, `SignInRequiredException`, which derives from it) or a `ClientOutdatedException` carrying the server's hash, and the error page is shown; with the installed app's sign-in composed, a `401` that remains goes to the sign-in route instead (SIGNIN-08).

### DESKTOP-30: Only a server address makes the loaders remote

- **Given** the desktop app
- **When** it is composed without `Aioros:Server`
- **Then** no remote loader results are registered and the loaders run in process as before (DESKTOP-17)
- **When** it is composed with `Aioros:Server`
- **Then** the remote loader results are registered with a client for that address.

### DESKTOP-34: Remote not found renders from the not found answer

- **Given** the remote navigator (DESKTOP-27) and a not-found page inside a layout with a loader, served by a server that has the same not-found page
- **When** it goes to a page whose loader answers not found, or to a path that matches no page
- **Then** it makes one request, to `/_data{path}` with the page's contract (the not-found page's when no page matches), and shows the not-found page with its layout's data from the `404` answer, which carries the not-found page's segments loaded for that path and its contract; it never asks for the not-found page's data with the not-found page's contract on the page's path (the server would answer `426`), and a not-found answer whose contract differs fails the navigation with `ClientOutdatedException`.

*Precedent: Remix single fetch answers a loader's thrown `404` with the data the error boundary renders in the same response; SvelteKit's `__data.json` carries the error and the layouts' data together.*

### DESKTOP-35: Failures without an error page show the built in error view

- **Given** an app without an `Error` page
- **When** a navigation fails (a loader throws, or the server answers `401`)
- **Then** `Failed` is raised and the window shows the built-in error view, a centered "Something went wrong" heading and "This page could not be shown." below it, inside no layout, never a blank window
- **When** an `Error` page whose own loading fails
- **Then** the built-in error view is shown as well and `Failed` is raised once, with the navigation's exception.

*Precedent: Next.js and React Router render a default error boundary when the app defines none.*

### DESKTOP-47: Remote view commands load only that view

- **Given** the remote navigator (DESKTOP-27) on a page with a view instance
- **When** a command inside the view succeeds
- **Then** the navigator asks `/_data{route}` with `X-Aioros-View: {key}` (SSR-77), only that view's loader runs on the server, and the view model receives the server's fresh data.

### DESKTOP-49: Remote commands refresh the views whose keys the server invalidated

- **Given** the remote navigator (DESKTOP-27) on the page of DESKTOP-48
- **When** C's command calls a server function over HTTP that answers `X-Aioros-Revalidate: issues`
- **Then** the keys read come from `/_data`, the navigator asks `/_data{route}` for A and C alone (`X-Aioros-View`), only their loaders run on the server, and their view models take the server's data while B's is kept.

### DESKTOP-56: Remote live pages refetch the views whose keys the server invalidated

- **Given** the remote navigator (DESKTOP-27) on a live page whose `/_data` answer says A read `sales` and B read `other`
- **When** the server invalidates `sales`
- **Then** the navigator, subscribed to `/_live?keys=other,sales` (the mounted views' keys, sorted), asks `/_data{route}` for A alone (`X-Aioros-View`), A's view model takes the server's data, B's is kept and `LiveRefreshed` is raised; navigating to a page that is not live ends the subscription on the server.

*Precedent: React Router's `useRevalidator` on a socket message; Turbo 8 refreshes that refetch with the client's own session.*

### DESKTOP-85: A refused live stream fails the navigator

- **Given** the remote navigator (DESKTOP-27) on a live page
- **When** its `/_live` request answers `401` after the handler's refresh
- **Then** the navigator raises `Failed` with `SignInRequiredException` and shows the error page (the sign-in route when the app signs in), as a `401` on `/_data` does (DESKTOP-29).

## Desktop container

### DESKTOP-31: The desktop takes the apps container

- **Given** the app composed for the desktop (`AiorosApplication.CreateBuilder` with `--desktop`) and built with `UseServiceProviderFactory` and another container (Autofac)
- **When** the navigator shows a page with a loader and then another page
- **Then** the app's services are that container's, the loader and the view model get their services from scopes of that container, and leaving the page disposes its scope.

### DESKTOP-32: A scoped messenger is shared within a page only

- **Given** an `IMessenger` registered as scoped and a page and its layout whose view models both take it
- **When** the navigator shows the page and then the page again
- **Then** both view models of one page get the same messenger and the next page gets another one.

### DESKTOP-33: Development rejects a singleton view model that captures the navigator

- **Given** the app composed for the desktop in the `Development` environment with a page view model registered as a singleton that takes `INavigator`
- **When** the app is built
- **Then** building fails because the singleton would capture a page's scoped navigator (`ValidateOnBuild`), and resolving `INavigator` from the root fails (`ValidateScopes`), as on the web (SSR-64); in `Production` the same app builds.

### DESKTOP-37: Page scopes are disposed asynchronously

- **Given** a page whose view model takes a scoped service that implements only `IAsyncDisposable`
- **When** the navigator leaves the page
- **Then** the page's scope is disposed with `DisposeAsync` and the service's `DisposeAsync` runs, without an exception; the same holds for a page left because its view model redirects.

### DESKTOP-38: A kept layout keeps the scope that created it

- **Given** a layout whose view model takes new data (`Receive`) and a scoped service
- **When** the navigator goes from one page in that layout to a second and back to the first
- **Then** the layout's view model is kept with the scope that created it, which stays undisposed while the layout is mounted, each page after the first gets a new scope, and the second page's scope is disposed when it is left; when the navigator then goes to a page outside the layout, the layout's scope and the current page's are disposed.

*Precedent: Next.js and React Router keep a shared layout mounted, with its state, across navigations between its children, and tear it down when it is left.*

### DESKTOP-39: The desktop flag is a switch

- **Given** the app composed with `AiorosApplication.CreateBuilder` and the arguments `--desktop --environment=Development`
- **When** the builder is created
- **Then** it runs on the desktop and its environment is `Development`: `--desktop` is a switch that takes no value from the next argument, wherever it appears.

## Desktop app navigation

### DESKTOP-40: Commands navigate through the apps own interface

- **Given** the app composed for the desktop (`AiorosHost.CreateBuilder` with `--desktop`) with its own navigation interface registered by `builder.AddNavigation<TInterface>`
- **When** a page's command navigates through it
- **Then** the window's navigator goes to that path, as with `INavigator` (DESKTOP-13)
- **When** an interface without a generated implementation is registered
- **Then** registering fails with an `InvalidOperationException` naming the interface and saying to call `AddNavigation` in the app's project.

## Host role

### DESKTOP-41: The role follows how the app starts

- **Given** the arguments and configuration an app starts with
- **When** its mode is read
- **Then** its role is `AiorosRole.Server` without `--desktop` (the website, also when precompiling), `AiorosRole.InProcess` with `--desktop` (the window runs the server's services itself, as in development) and `AiorosRole.Client` with `--desktop` and `Aioros:Server` set (the installed app working against a remote server); and `builder.Role` of the app's builder is its mode's role.

*Precedent: Dioxus and Leptos compile server code only with `cfg(feature = "server")`; Blazor tells its host apart with `OperatingSystem.IsBrowser` and `RendererInfo`.*

### DESKTOP-42: Server registrations are skipped in the client

- **Given** a service and a hosted service registered inside `builder.Server(services => ...)`
- **When** the app is built and started as the website, in process and as the installed client
- **Then** on the website and in process the service is registered and the hosted service runs, and in the installed client neither is registered and nothing of them runs.

### DESKTOP-43: Hosted services outside the server run in the client

- **Given** a hosted service registered outside `builder.Server(...)`
- **When** the installed client is started
- **Then** it runs there as on the server: only the registrations inside `builder.Server(...)` are skipped, so a service the app means for every host keeps working.

## Desktop view

### DESKTOP-45: Views are composed with their own models

- **Given** a page that places a view with `x:Class` and embeds another page with `a:View`, each with its loader
- **When** the navigator goes to it
- **Then** the window shows the page with each view bound to the view model created from its own loader's data, the window's title is the page's, and a view whose loader fails shows the error message in its place while the rest of the page is shown.

## Desktop view command

### DESKTOP-46: A view command refreshes only that view

- **Given** a page with a loader and two instances of the same view with loaders
- **When** a button inside the second instance is clicked and its command succeeds
- **Then** only that instance's loader runs again, its view model receives the data (`Receive`) or the instance's control gets the new view model as its `DataContext`, the page's and the first instance's view models and `DataContext`s are untouched, and `CommandSettled` is raised with `true`; a second click in the same instance while its loader runs cancels the first one's token and discards its result (DESKTOP-26 per instance), while a click in the other instance cancels nothing; a page button still revalidates the whole page (DESKTOP-23).

### DESKTOP-48: A view command refreshes the views whose keys it invalidated

- **Given** the in-process navigator on a page with a page loader and three views (A reads `issues`, B reads `other`, C's button runs a command), each loader counting its runs
- **When** C's command succeeds after a server function invalidated `issues`
- **Then** only A's and C's loaders run again, their view models take the data, B's and the page's view models and `DataContext`s are untouched, and `CommandSettled` is `true`; the keys each view read are kept per mounted view from its last load; when the command called a function without key information, every loader runs again; when it called none, only C's runs.

## Desktop repeated view

### DESKTOP-50: An items command refreshes that item alone

- **Given** the in-process navigator on a page whose list's item template places a view with a batch loader and a button
- **When** the page is shown
- **Then** each item's view control has its own view model from the batch loader's single run
- **When** the second item's button is clicked and its command succeeds
- **Then** the batch loader runs again with that item's key alone, that item's control takes the new view model (or its view model receives the data), the other items' view models and `DataContext`s are untouched, and `CommandSettled` is `true`.

### DESKTOP-51: Remote item commands ask for that item alone

- **Given** the remote navigator (DESKTOP-27) on that page
- **When** it is shown
- **Then** each item's view model comes from the `items` of `/_data` (SSR-88)
- **When** the second item's command succeeds
- **Then** the navigator asks `/_data{route}` with `X-Aioros-View: {key}[{item}]` and only that item's view model takes the server's data.

## Installed app host

### DESKTOP-52: The desktop lifetime shuts down when the host stops

- **Given** the desktop app started with its host
- **When** the host is asked to stop (`SIGTERM` makes `ConsoleLifetime` call `StopApplication` and log "Application is shutting down...", and cancels the signal's default termination)
- **Then** the window's desktop lifetime is shut down on the UI thread, so `DesktopHost.Run` returns and the process exits, as closing the window does.

### DESKTOP-53: The baked origin is the installed apps server outside development

- **Given** an app whose build baked in a server origin (`AiorosServer`, GENERATOR-55)
- **When** its mode is read with `--desktop` outside `Development`
- **Then** its server is the baked origin and its role `AiorosRole.Client`
- **When** `Aioros:Server` is configured
- **Then** the configured server wins; in `Development` the baked origin is ignored, so the desktop runs in process or against the dev server named by `Aioros:Server`; without `--desktop` the role stays `AiorosRole.Server`.

*Precedent: Expo's `origin` and the dev-server URL Dioxus injects in development.*

## Command line

### DESKTOP-54: Standard settings reach the host with or without the desktop switch

- **Given** the app composed with `AiorosApplication.CreateBuilder`
- **When** the builder is created from `--urls http://127.0.0.1:5231 --environment Staging --contentRoot <folder> Shop:Name=games --Shop:Owner ana`
- **Then** the web host's configuration has those URLs, the environment `Staging`, that content root and both settings
- **When** the same arguments follow `--desktop` and a deep link (`shop://open?path=%2F`)
- **Then** the app runs on the desktop with the same URLs, environment, content root and settings, and the deep link is no setting: only the Aioros switches (`--desktop`, `--path=`, `--precompile=`) and deep links are interpreted, and a value after a `--key` is always that key's value, even when it is a URL.

## Desktop live

### DESKTOP-55: Invalidated keys refresh the views of a live page

- **Given** the in-process navigator on a live page (`a:Web.Live`) whose views A and B read `sales` and `other`
- **When** a background service invalidates `sales` outside any command
- **Then** only A's loader runs again, A's view model takes the data, B's and the page's models and `DataContext`s are untouched, and `LiveRefreshed` is raised; while the page is shown the navigator watches the app's data versions once (`Watchers` is 1), and after navigating to a page that is not live it watches none, so invalidations run no loader.

*Precedent: Phoenix LiveView subscribes in `mount` and unsubscribes with the process.*

### DESKTOP-84: Reactivating the window wakes the live reconnection

- **Given** a navigator whose window has the app's `LiveReconnection` (from its services) waiting to reconnect
- **When** the window is activated
- **Then** the wait ends at once (the network becoming available, `NetworkChange.NetworkAvailabilityChanged`, wakes it the same way, until the window closes).

## Live reconnection

### DESKTOP-77: Waits double up to the cap with full jitter

- **Given** the installed app's `LiveReconnection` with a random source that answers `r`
- **When** it computes the wait before attempt `n` (counted from 0)
- **Then** the wait is `r × min(30 s, 1 s × 2ⁿ)` (1 s, 2 s, 4 s … capped at 30 s, AWS's "full jitter"); when the server sent a `retry:` interval, that interval replaces the 1 s base (and is the cap when it exceeds 30 s); a huge `n` still waits at most the cap, and a `retry:` of zero keeps the 1 s base.

*Precedent: the HTML EventSource reconnection time, SignalR's and Phoenix's reconnect backoff.*

### DESKTOP-78: Failures wait longer and a reconnect revalidates everything

- **Given** a `RemoteLiveKeys` watch whose `/_live` request answers `503`, then fails with a network error, then opens (`200`, `open` event, a `keys` event), with the random source at `0.5`
- **When** it is watched
- **Then** it waits `0.5 s` on the injected `TimeProvider` before the second request and `1 s` before the third (no request is sent before the time is advanced), and once the stream opens again it reports one invalidation of everything (the whole page revalidated through `/_data`, as Turbo 8 refreshes and LiveView remounts on reconnect, instead of replaying events) before the stream's own keys.

### DESKTOP-79: A successful reconnect starts the backoff again

- **Given** a `RemoteLiveKeys` watch that reconnected after two failed attempts
- **When** the reopened stream ends
- **Then** the next wait is back to the first step (`r × 1 s`), not the third.

*Precedent: SignalR and Phoenix reset their backoff after a successful connection.*

### DESKTOP-80: The servers retry interval is the base of the wait

- **Given** a `RemoteLiveKeys` watch whose stream sent `retry: 5000` before it ended
- **When** it reconnects
- **Then** it waits `r × 5 s` (the server's reconnection time as the base); a later stream without `retry:` keeps that base.

*Precedent: the HTML EventSource `retry` field.*

### DESKTOP-81: A refusal that remains after a refresh ends the watch signed out

- **Given** a `RemoteLiveKeys` watch over the installed app's `SignedInHandler` whose `/_live` request answers `401`
- **When** it is watched
- **Then** the handler refreshes the token once and sends the request again, and when that one is refused as well the watch ends with `SignInRequiredException`, sends no further `/_live` request and waits on nothing.

### DESKTOP-82: Refusals and no content end the watch

- **Given** a `RemoteLiveKeys` watch whose `/_live` request answers `403`, `204`, or another client error (`400`, `404`)
- **When** it is watched
- **Then** the watch ends without an error after that one request and waits on nothing (the HTML EventSource stops at `204` and at a refusal).

### DESKTOP-83: A wake reconnects at once

- **Given** a `RemoteLiveKeys` watch waiting to reconnect after failures
- **When** its `LiveReconnection` is woken (the network became available or the window was reactivated)
- **Then** `Wake` returns `true` and the watch requests `/_live` again at once without the time advancing.

*Precedent: React Query's `refetchOnReconnect` and `refetchOnWindowFocus`, the browser's `online` and `visibilitychange` events.*

### DESKTOP-90: Timeouts and too many requests retry after the backoff

- **Given** a `RemoteLiveKeys` watch whose `/_live` request answers `408` or `429` without `Retry-After`, then opens, with the random source at `0.5`
- **When** it is watched
- **Then** it waits `0.5 s` on the injected `TimeProvider` (the backoff, as after a `5xx`) and requests `/_live` again instead of ending, and the reopened stream reports everything invalidated.

*Precedent: RFC 9110 (408: the client may repeat the request) and RFC 6585 (429), SignalR's and gRPC's retry policies, which retry both.*

### DESKTOP-91: The servers retry after is the least wait and a wake does not cut it short

- **Given** a `RemoteLiveKeys` watch whose `/_live` request answers `429` or `503` with `Retry-After: 120`, then opens, with the random source at `0.5`
- **When** it is watched and its `LiveReconnection` is woken during the wait
- **Then** it requests `/_live` again only once `120 s` have passed on the injected `TimeProvider` (the server's wait replaces the shorter backoff), and `Wake` returns `false`: it ended no wait.

*Precedent: RFC 9110 section 10.2.3, gRPC's `grpc-retry-pushback-ms`, Polly's `Retry-After` delay generator.*

### DESKTOP-92: Retry after is a wait of at most five minutes and never shortens the backoff

- **Given** a `LiveReconnection` on a clock
- **When** it reads a `Retry-After` header
- **Then** an HTTP-date 90 s ahead of the clock is a wait of `90 s`, a delta of `600` seconds is capped at `5 min`, and a date already past or no header is no wait; and a backoff of `0.5 s` with a `Retry-After` of `2 s` waits `2 s` once, while with no `Retry-After` it waits the `0.5 s` (the longer of the two, never less than the backoff).

## Settled frames

### DESKTOP-94: A capture keeps the first frame the page settles on

- **Given** a desktop capture (`AIOROS_DESKTOP_CAPTURE`) of a page whose frames change for a while after navigating (charts' enter animations)
- **When** the capture runs
- **Then**
  - It moves the page on one frame at a time and keeps the first frame that the next five frames leave unchanged (the page settled), disposing the others, instead of the frame right after navigating; a page that never settles is kept after 600 frames.
  - Settling is read from the frames, not from a fixed delay.

*Precedent: Playwright's and Storybook test-runner's screenshot stabilization (`toHaveScreenshot` retakes until two shots match).*

### DESKTOP-95: Frames taken while animations are pending never settle

- **Given** a desktop capture of a page whose animation starts after a begin delay (Pictor's pie, 400 ms), so its first frames are the same blank frame while Avalonia still has animation work pending
- **When** the capture runs
- **Then** frames taken while animation work is pending never count as settled: it keeps the first frame that the next five frames leave unchanged once no animation work is pending (the drawn pie), not the blank frame the delay repeats; a page whose animation work never ends is kept after 600 frames.

*Precedent: Playwright's `animations: "disabled"` and Storybook test-runner's `waitForPageReady`, which wait for the running and pending animations (`document.getAnimations`) before comparing shots.*

## Pending animations

### DESKTOP-96: Avalonia reports delayed animations and requested frames as pending

- **Given** a headless Avalonia window
- **When** a control's opacity changes under a `DoubleTransition` with a 400 ms delay
- **Then** `PendingAnimations.Active` is true before the delay has elapsed
- **When** the transition is removed and the next frames are rendered, it becomes false; when the window requests an animation frame, it is true until the frame that runs the callback is rendered
- **Then** false.

## Desktop hot reload

### DESKTOP-98: A reloaded source keeps its models and view state

- **Given** a page shown in the desktop window inside its layout, scrolled down, with the focus on a named text box and text typed in an unbound named one
- **When** one of its mounted sources is reloaded (`DesktopNavigator.Reload`, after an edit loads it over the compiled XAML)
- **Then** the window shows the edited XAML from that source down (the page alone for a page edit, the layout and the page for a layout edit) with the same view models, so no loader runs and what they hold stays; the scroll offset, the focused element and the unbound text are put back; a source the window does not show changes nothing, and reloading with no source rebuilds every mounted view.

*Precedent: HotAvalonia, which reloads a XAML file into the live control with its data context; Flutter's hot reload, which rebuilds the widgets and keeps their `State`.*

### DESKTOP-99: Saved sources and code edits reload the window

- **Given** the desktop app running in development from its project folder
- **When** a `.axaml` file under it is saved
- **Then** it is loaded over the compiled XAML and the window reloads it (DESKTOP-98)
- **When** it does not parse
- **Then** the window keeps the page and shows the error over it, with the file and the parser's message and line, until a save that parses clears it, the text it had before the error included
- **When** the runtime applies a C# edit (`dotnet watch`'s `MetadataUpdateHandler`)
- **Then** every mounted view is rebuilt with the same view models, so the edited code shows without a restart; outside development nothing is watched.

*Precedent: HotAvalonia's file watcher and Avalonia's own hot reload driven by `dotnet watch`.*

## Reveal

### DESKTOP-100: Revealed controls play again when they first scroll into view

- **Given** a native window whose page has two controls marked `a:Web.Reveal` with an entrance animation, one in its scroll viewer's view and one below it
- **When** the page opens
- **Then** both run their animation; when the viewer scrolls the lower one into view, it plays its animation again from its start (opacity 0) while the upper one is left as it is, and scrolling away and back does not play it a third time, as the web plays it (MOTION-23).

## Sweep

### DESKTOP-101: Swept gradients move on each frame

- **Given** a native window with a text whose foreground is a gradient from a shared resource and `a:Web.Sweep="4"`
- **When** frames are drawn
- **Then** the text is filled with its own copy of the gradient, twice as wide and repeating, moved further to the right on each frame (one whole gradient every 4 seconds), as the web sweeps it (MOTION-26), and the shared resource's brush is left as it was.

## Count

### DESKTOP-102: Numbers count up to their text

- **Given** a native window with a text `85,620` marked `a:Web.CountUp`
- **When** its page opens with the text in view
- **Then** the text counts up through values in its own format (grouped with commas, below 85,620) and ends on exactly `85,620`, as the web counts it (MOTION-28).

## Marquee

### DESKTOP-103: Marquees move left on each frame

- **Given** a native window with a horizontal panel marked `a:Web.Marquee`
- **When** frames are drawn
- **Then** the panel moves further left on each frame, never more than half its width, as the web moves it (MOTION-30).

## Spotlight

### DESKTOP-104: The glow follows the pointer

- **Given** a native window with a card marked `a:Web.Spotlight`
- **When** the pointer moves over it
- **Then** an adorner over the card, which takes no input, glows with a radial gradient of that color centred where the pointer is; when the pointer leaves, the glow goes, as the web's (MOTION-31).

## Parallax

### DESKTOP-105: Layers shift against the scroll by their share

- **Given** a native window with a layer marked `a:Web.Parallax="0.5"` in a scroll viewer
- **When** the viewer scrolls
- **Then** the layer is shifted by half its distance from the viewport's middle, against the scroll, as the web shifts it (MOTION-34).
