# Installed app handoff

The website opens the installed desktop app on the same page through a deep link, and the installed app keeps itself up to date. An app becomes installable by naming its URL scheme, the MSBuild property `AiorosScheme`. A deep link carries only a navigation path, checked against the app's routes, never a token (`<scheme>://open?path=/orders/42`, RFC 8252 section 8.1: any app can invoke or claim a scheme). Windows and Linux start a new process with the URL, so the app is single-instance and forwards its arguments to the running instance over a named pipe, as VS Code and Tauri's single-instance plugin do; macOS delivers the URL to the running app through Avalonia's `IActivatableLifetime`. The scheme is registered at run time on Windows (`HKCU\Software\Classes`, as Tauri's deep-link plugin does) and Linux (a `.desktop` file with `x-scheme-handler`, as Tauri does), and at build time on macOS (`CFBundleURLTypes`). Installers and updates come from Velopack.

22 criteria. Each has an identifier and exactly one test.

## Deep link

### INSTALL-01: A deep link carries only a known path

- **Given** the scheme `shop` and an app with the pages `/`, `/orders` and `/orders/[id:int]`
- **When** a deep link is read
- **Then** `shop://open?path=%2Forders%2F42`, `SHOP://open/?path=/orders/42` and `shop://open?path=%2Forders%3Fq%3Dlate` give the paths `/orders/42`, `/orders/42` and `/orders?q=late`; and it is rejected (no path) when its scheme is another one, its host is not `open`, it has user information, a port, a path other than `/` or a fragment, its query has anything but one `path` (a second `path`, a `token` or a `code` beside it), or the path is empty, relative, protocol-relative (`//evil.example`), holds a backslash or a control character, is an absolute URL, or matches no page (`/orders/abc`, `/admin`).

### INSTALL-02: The first accepted deep link chooses the first path

- **Given** the arguments the desktop app starts with
- **When** its first path is chosen
- **Then** it is the path of the first deep link in the arguments that is accepted (INSTALL-01), and otherwise the start path (`--path=` or `/`); a rejected deep link is ignored and named in the returned reason.

## Single instance

### INSTALL-03: A second instance forwards its arguments to the running one

- **Given** a pipe name no instance holds
- **When** a first instance claims it
- **Then** it becomes the running instance
- **When** a second instance claims the same name with the arguments `["shop://open?path=/orders/42"]`
- **Then** the claim answers that it forwarded them and the running instance reads exactly those arguments from its channel; a third claim with no arguments is read as an empty list; and once the running instance is disposed, the next claim becomes the running instance.

### INSTALL-04: A stale socket is replaced

Given, on Linux or macOS, the socket file a killed instance left behind (bound, never unlinked, nobody listening), when an instance claims that name, then it removes the stale socket and becomes the running instance, as VS Code does after `ECONNREFUSED`.

## Desktop handoff

### INSTALL-05: A forwarded deep link opens its page

- **Given** the desktop navigator on `/` of an app with the scheme `shop` and its running instance
- **When** a second instance forwards `shop://open?path=%2Fitems%2F7`
- **Then** the window shows `/items/7`
- **When** it forwards a deep link that is rejected, or arguments without one
- **Then** the window stays on its page.

### INSTALL-11: Open in app links are hidden on the desktop

- **Given** a page with a link marked `a:Web.OpenInApp="True"`
- **When** the desktop navigator shows it
- **Then** that link is hidden, since the window already is the installed app.

### INSTALL-12: An outdated client updates and restarts on the path

- **Given** the navigator composed with an update handler
- **When** a navigation fails with `ClientOutdatedException` (the server answered `426`)
- **Then** the handler is asked to update and restart on that path (the restarted app gets the arguments it started with, its `--path=` and deep links replaced by `--path={path}`), and when it restarts, neither `Failed` nor the error page follows
- **When** it finds no update
- **Then** the navigation fails as before (`Failed` and the error page, DESKTOP-29); without a handler, a `426` fails as before.

### INSTALL-14: A deep link to a protected page signs in and lands there

- **Given** the installed app against a server with a protected page `/mine` and no stored session
- **When** a second instance forwards `shop://open?path=%2Fmine`
- **Then** the app signs in through the browser (SIGNIN-08) and lands on `/mine` showing the signed-in user's data.

### INSTALL-17: The first run signs in and lands on the page the user came from

- **Given** the installed app's first run after its install (Velopack's first run) with the installed-app sign-in
- **When** its first path is chosen
- **Then** it is the sign-in route with the start path as `ReturnUrl` and `FirstRun=true`, and otherwise the start path
- **When** the desktop navigator on that route against the server, where the browser holds the return path `/items/7`, when the browser sign-in completes
- **Then** the window shows `/items/7`
- **When** the server sends no return path, or one that matches no page
- **Then** it shows the `ReturnUrl`.

### INSTALL-19: The installed app downloads its update from the site

- **Given** the site serving a feed folder that holds release 1.0.1 of `Shop` on a local port
- **When** Velopack's update manager of `Shop` 1.0.0 installed (a test locator) checks `{site}/updates/`
- **Then** it finds 1.0.1 and downloads its full package, whose hash Velopack checks, into its packages folder.

## Scheme registration

### INSTALL-06: Linux registers the scheme in the given xdg directories

- **Given** a temporary `XDG_DATA_HOME` and `XDG_CONFIG_HOME` (never the user's), with a `mimeapps.list` that already holds another default
- **When** the scheme `shop` is registered on Linux for `/opt/Shop/Shop.AppImage`
- **Then** `applications/shop-handler.desktop` is written with `Exec="/opt/Shop/Shop.AppImage" %u`, `MimeType=x-scheme-handler/shop;` and `NoDisplay=true`, `mimeapps.list` gains `x-scheme-handler/shop=shop-handler.desktop` under `[Default Applications]` and keeps the other default, and `xdg-mime query default x-scheme-handler/shop` answers `shop-handler.desktop` with those directories; registering again changes no file and reports nothing changed, and registering after the app moved rewrites the `Exec` line.

### INSTALL-07: Windows registers the scheme under the current users classes

- **Given** the scheme `shop`, the title `Shop` and the executable `C:\Apps\Shop\Shop.exe`
- **When** the Windows registration is planned
- **Then** it is the key `Software\Classes\shop` under the current user with the default value `URL:Shop` and an empty `URL Protocol` value, and `Software\Classes\shop\shell\open\command` with the default value `"C:\Apps\Shop\Shop.exe" "%1"`, which the registrar writes only where a value differs.

## Mac bundle

### INSTALL-08: The mac bundle declares the scheme

- **Given** a project with `AiorosScheme` set to `shop`
- **When** its `AiorosMacBundle` target runs for `osx-arm64`
- **Then** the publish folder gets an `Info.plist` naming the executable, the bundle identifier, the version and `CFBundleURLTypes` with the scheme `shop`, with every value XML-escaped; without `AiorosScheme`, or for another runtime, no `Info.plist` is written.

## Open in app script

### INSTALL-09: Open in app tries the scheme and falls back to the link

- **Given** a page at `/orders/42` of an app with the scheme `shop`, then its document head carries `<meta name="aioros-app" content="shop">`, which an app without a scheme does not get; given on it a link to `/download` marked to open in the app (`data-open-app`)
- **When** it is clicked in Chromium
- **Then** the browser is asked to open `shop://open?path=%2Forders%2F42` and the page stays
- **When** 1.5 seconds pass with the page focused and visible (no handler took the link)
- **Then** the page navigates to `/download?return=%2Forders%2F42`, the link carrying the path it came from
- **When** a fake handler takes the link (the window loses focus, as when the operating system opens the app)
- **Then** no navigation follows; with JavaScript disabled, the link goes to `/download`
- **When** the built-in stand-in of a native-only page at `/files` (NATIVE-09), which carries the same meta and the same script, when its "Open in the app" is clicked and no handler takes it
- **Then** the browser is asked to open `shop://open?path=%2Ffiles` and, 1.5 seconds later, `/download?return=%2Ffiles`.

## Open in app

### INSTALL-10: Open in app links are marked for the script

- **Given** `a:Web.OpenInApp="True"` on a `HyperlinkButton` with a `NavigateUri` and on a panel with `a:Web.Href`
- **When** the page is compiled
- **Then** each `<a>` carries `data-open-app` beside its `href`, and a link without it carries none.

## Installed mode

### INSTALL-13: The installed app runs on the desktop and deep links are no settings

- **Given** the desktop app's mode
- **When** it is read from arguments with `--desktop` and a deep link
- **Then** the deep link is not given to the configuration as a setting
- **When** the app was published as an installer (the runtime switch `Aioros.Installed`)
- **Then** it runs on the desktop without `--desktop`.

### INSTALL-20: The update feed is the servers unless configured

- **Given** the installed app's configuration
- **When** its update feed is chosen
- **Then** it is `Aioros:Updates` when set, otherwise the server's `/updates/` (`https://shop.example/updates/` for `Aioros:Server` `https://shop.example`), and none without a server.

## Return path

### INSTALL-15: The download link keeps the return path in a short lived cookie

- **Given** an app with the installed-app sign-in and the pages `/orders/[id:int]` and `/download`
- **When** the browser asks `/download?return=%2Forders%2F42%3Fq%3Dlate` (the link INSTALL-09 follows)
- **Then** the answer sets a short-lived cookie that holds the path `/orders/42?q=late` and never a token: `HttpOnly`, `SameSite=Lax`, scoped to the authorization endpoint's path and expiring after `ReturnPathLifetime` (an hour by default); a `return` that is not a local path matching one of the app's pages (`//evil.example`, `https://evil.example/`, `/admin`, `/orders/abc`), or a `return` on another page, sets no cookie.

### INSTALL-16: The authorization response carries the return path when asked

- **Given** a browser holding the return path cookie for `/orders/42` and signed in with the website's cookie
- **When** the installed app's authorization request asks for the return path (`return_path=request`)
- **Then** the redirect to the loopback carries `return_path=/orders/42` beside the code and the cookie is deleted
- **When** the request does not ask for it
- **Then** no `return_path` is sent and the cookie is kept; a cookie whose path matches no page sends nothing.

### INSTALL-21: The return path cookie is secure only over HTTPS

- **Given** the download page's return path kept in its cookie (INSTALL-15)
- **When** the download page is asked for over HTTPS
- **Then** the cookie is marked `secure`
- **When** it is asked for over plain HTTP (a LAN address, where a browser drops a `Secure` cookie)
- **Then** the cookie is not marked `secure`, so the browser keeps it.

*Precedent: ASP.NET Core's `CookieSecurePolicy.SameAsRequest`, the default of its antiforgery and session cookies.*

### INSTALL-22: The return path cookie is secure behind a tls proxy

- **Given** the download page's return path kept in its cookie (INSTALL-15)
- **When** the download page is asked for over plain HTTP carrying `X-Forwarded-Proto: https` (behind a TLS-terminating proxy, trusted or not)
- **Then** the cookie is marked `secure`, so it never travels over plain HTTP once the visitor's scheme is HTTPS.

## Update feed

### INSTALL-18: The site serves the feed with its types and caching

- **Given** a feed folder holding `releases.linux.json`, `assets.linux.json`, `RELEASES`, `Shop-1.0.1-linux-full.nupkg`, `Shop-1.0.1-linux-delta.nupkg`, `Shop.AppImage`, `Shop-win-Setup.exe`, `Shop-win-Portable.zip` and `notes.txt`, configured as `Aioros:UpdatesFolder`
- **When** the site is asked for them under `/updates/`
- **Then**
  - The release index files answer `application/json` (or `text/plain` for `RELEASES`) with `Cache-Control: no-cache`, the packages `application/octet-stream` with `public, max-age=31536000, immutable`, the AppImage `application/vnd.appimage`, the installer `application/vnd.microsoft.portable-executable` and the portable zip `application/zip`, each with `no-cache`; a range request is answered `206`
  - `notes.txt`, `/updates/` itself and a missing file answer `404`; without the setting `/updates/` answers `404`.
