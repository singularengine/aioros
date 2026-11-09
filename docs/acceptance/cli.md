# Command line

The `aioros` tool (`src/Aioros.Cli`), installed with `dotnet tool install -g Aioros.Cli`. Each command is planned as a `dotnet` invocation, so the plan is tested without running it.

18 criteria. Each has an identifier and exactly one test.

## Planner

### CLI-01: Usage is printed

- **Given** no command or an unknown one
- **When** `aioros` runs
- **Then** it prints the usage listing `new`, `dev`, `build`, `start` and `routes`, and exits with 1 for an unknown command.

### CLI-02: New installs the template and creates the app

- **Given** `aioros new shop`
- **When** it is planned
- **Then** it installs the `Aioros.Templates` package of the tool's version and creates the app with `dotnet new aioros -n shop`.

### CLI-03: Dev build and start are dotnet commands

- **Given** `aioros dev`, `aioros build` and `aioros start` in an app folder whose project is `Shop/Shop.csproj`
- **When** they are planned
- **Then**
  - They are `dotnet watch --project Shop/Shop.csproj --non-interactive` in development, with the app's address after `--` (CLI-10)
  - `dotnet publish Shop/Shop.csproj -c Release -o .aioros/publish` followed by the published app in production with `--precompile=aioros.plans.json`, run from `.aioros/publish`; and the published app in production, run from `.aioros/publish`, so it reads its published settings.

### CLI-04: Routes are listed and conflicts fail

- **Given** an app folder with pages
- **When** `aioros routes` runs
- **Then** it prints each route with its page and layouts, sorted by route, and exits with 0
- **When** two pages on the same route
- **Then** it prints the conflict and exits with 1.

### CLI-05: The app is the project with pages

- **Given** an app folder (the solution folder, with the app's project in a subfolder, as `dotnet new aioros` creates it, or the project's own folder)
- **When** the app is looked for
- **Then** it is the project, at the root or one folder down, that has a `Pages` folder
- **When** none
- **Then** `dev`, `build`, `start` and `routes` print that no Aioros app was found and exit with 1
- **When** several
- **Then** they list them and exit with 1.

### CLI-07: Build bakes in the server of the installed app

- **Given** `aioros build --server=https://shop.example` in an app folder
- **When** it is planned
- **Then** the publish step carries `-p:AiorosServer=https://shop.example`, so the installed app built from it works against that server without configuration (GENERATOR-53); without `--server` the publish step is unchanged and the project's own `AiorosServer` property, if any, applies; a value that is not an absolute `http` or `https` URL prints an error and exits with 1.

### CLI-08: Build installer publishes and packs with velopack

- **Given** `aioros build --installer --version=1.2.0` in an app folder whose project is `Shop/Shop.csproj`
- **When** it is planned on `linux-x64`, `win-x64` and `osx-arm64`
- **Then** it publishes self-contained for that runtime to `.aioros/installer/publish` with `-p:AiorosInstalled=true` and `-p:Version=1.2.0` (and `-p:AiorosServer` when `--server` is given), and then packs it with Velopack's `vpk` of the version the framework references (`dotnet dnx vpk@<version> --yes pack`), with the pack id `Shop`, the version, the main executable (`Shop`, or `Shop.exe` on Windows) and `.aioros/installer/releases` as the output, adding `--plist` with the generated `Info.plist` on macOS (INSTALL-08); without `--version`, or with a version that is not semantic (`major.minor.patch`, optionally with a pre-release), it prints an error and exits with 1.

### CLI-10: Dev listens on a free port without opening a browser

- **Given** `aioros dev` in an app folder
- **When** it is planned
- **Then** `dotnet watch` runs with `DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER=1` (no browser is opened for the developer) and `DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH=1` (the development channel refreshes the page in place, SSR-09 and SSR-284; `dotnet watch`'s own script would reload it whole, losing its state), the app gets the tool's development token (`Aioros__DevelopmentToken`), with which the tool posts the build's errors to it (CLI-11, SSR-289), and the app listens on `http://localhost` at the port of the project's launch profile (the first `Project` profile's first `http` `applicationUrl`, the address `dotnet run` would use: 5318 for the Gallery) or 3000 without one, passed as `--urls` after `--`; with `--port=4000` it listens on 4000
- **When** the port is taken
- **Then** the next free one of the ten after it is used and `Port 3000 is in use, using 3001 instead.` is printed
- **When** none of them is free, or the port is not a number from 1 to 65535
- **Then**
  - The reason is printed and the command exits with 1.
  - With `--desktop`, the app runs as its desktop window under `dotnet watch` (`-- --desktop`, its hot reload DESKTOP-99), with no address and no port.

*Precedent: `next dev` ("Port 3000 is in use, trying 3001 instead.", `-p`), Vite's `server.port`, which tries the next port unless `strictPort`.*

### CLI-42: A command followed by help prints its usage and runs nothing

- **Given** `aioros new`, `dev`, `build`, `start` or `routes` followed by `--help` or `-h`, with or without an app in the folder
- **When** it is planned
- **Then**
  - It prints that command's usage with its options (`dev`: `--port=<n>` and `--desktop`; `build`: `--server=<url>`, `--installer` and `--version=<semver>`), exits with 0 and runs nothing, as `dotnet watch --help` does
  - `aioros new --help` does not create an app named `--help`.

## Template app

### CLI-06: The template creates an app that builds and precompiles

- **Given** the framework's packages and the template package packed to a local feed
- **When** `dotnet new aioros` creates an app from it in a private template hive (`--debug:custom-hive`) and the app is restored into a private package cache, built and run with `--precompile=<file>`
- **Then**
  - The build reports 0 warnings and 0 errors, the run exits with 0 and reports every page precompiled (`Precompiled N of N pages`, N > 0), and nothing is written to the user's template hive or NuGet cache.
  - The app is also quiet under `dotnet watch`: its evaluated `AdditionalFiles` name each XAML file once (Avalonia 12.1 adds them itself, and a second entry makes `dotnet watch` warn `Duplicate source file` for every page), its settings keep ASP.NET Core's per-request logs out (`Microsoft.AspNetCore` at `Warning`), and its launch profile does not open a browser.
  - Its XAML records its elements' positions in every configuration (`AvaloniaXamlCreateSourceInfo`, which Avalonia sets only in Debug), so a Release precompilation names the XAML line that kept a plan (ENGINE-306, PRODUCTION-11).
  - A plain project that references the app and uses its generated `AiorosFunctions` (a test project) builds as well, since the framework's generators leave it alone (GENERATOR-400).

*Precedent: the ASP.NET Core templates are tested by creating, building and running each one from the packed template package (`dotnet/aspnetcore` `src/ProjectTemplates/test`).*

## Production app

### CLI-09: A precompiled app serves each culture without loading Avalonia

- **Given** Lumen published and precompiled with `Aioros__Cultures=en-US,fr-FR` (the precompile step of `aioros build`)
- **When** it runs in production with the same setting
- **Then** the plans file holds every precompiled page for both cultures and both themes, the report names the cultures, and requests with `Accept-Language: fr-FR` and `en-US` are answered with `<html lang>` and `Content-Language` of their culture without Avalonia loaded.

## Build output

### CLI-11: Compile errors are read from the watch output

- **Given** `aioros dev` running `dotnet watch`
- **When** its output is read line by line (and echoed unchanged)
- **Then**
  - A C# edit that does not compile yields the build's errors: each `error` line with a location (`/app/Models/HomeModel.cs(11,19): error CS1002:; expected`, with or without `dotnet watch`'s prefix, the project in brackets and terminal colors, which the tool keeps when it writes to a terminal) as its file, line, column, code and message, the list growing with each error line after a file change and starting again with the next change; then, once `dotnet watch` reports the edit applied (`changes applied`, `No managed code changes to apply`) or a build succeeding, the cleared list, once; warnings, errors without a location (`ENC0033`, a rude edit that restarts the app) and other lines yield nothing.
  - The tool sends each list to the app's development channel with the token it gave the app (CLI-10, SSR-289).

*Precedent: Next.js and Vite, whose dev server owns the build and pushes its errors to the overlay.*

## Library

### CLI-40: A project that bans the same APIs builds and keeps every rule

- **Given** a project that references Aioros (its `Aioros.targets`, which brings the framework's list of banned APIs), uses `Microsoft.CodeAnalysis.BannedApiAnalyzers` with warnings as errors, and has its own `BannedSymbols.txt` naming `Task.Wait` (also in the framework's list)
- **When** it is built
- **Then** no `RS0031` is reported and the build succeeds
- **When** its code calls `Task.Wait` and `Thread.Sleep(int)`
- **Then** `RS0030` is reported for each, the first with the project's own message, the second with the framework's: the symbols the project already bans leave the framework's list, so no rule is lost and no symbol is listed twice.

## Package version

### CLI-41: The version takes a suffix and samples are not packed

- **Given** the framework's packable projects
- **When** they are evaluated without a version
- **Then** their version is `0.1.0-preview.1`; with `-p:VersionSuffix=preview.914.abc` it is `0.1.0-preview.914.abc`, and with `-p:Version=1.2.3` it is `1.2.3`, for the framework and for the template package
- **When** every project under `samples`, when it is evaluated
- **Then** `IsPackable` is `false`, so `dotnet pack Aioros.slnx` packs the framework alone.

## Dev process

### CLI-43: Stopping the tool stops everything it started

- **Given** `aioros dev` running `dotnet watch`, which runs the app
- **When** the tool is told to stop (`SIGINT`, `SIGTERM` or `SIGHUP`, as `kill`, a closed terminal or a stopped service send)
- **Then** it stops `dotnet watch` and every process under it, so nothing it started is left behind, and exits; the child is started with the tool's process id in `Aioros__DevelopmentTool`.

*Precedent: `next dev` and Vite, which take their server down with them; .NET's `Process.Kill(entireProcessTree: true)`.*

### CLI-44: A taken port is reported first with its process

- **Given** `aioros dev` whose launch profile's port is taken
- **When** it runs
- **Then** the first line it prints, before anything from `dotnet watch`, says the port is in use, by which process when the machine can tell (Linux: `Port 3000 is in use by dotnet (pid 123), using 3001 instead.`), and which port it uses; where it cannot tell, the line names only the ports (CLI-10).

## Development tool

### CLI-45: An app stops when the tool that started it is gone

- **Given** an app in development whose `Aioros:DevelopmentTool` names the process id of the tool that started it
- **When** that process is gone (killed, or already gone at start)
- **Then** the app stops itself through `IHostApplicationLifetime`, so an app whose `dotnet watch` or tool was killed outright is not left serving its port; in production the setting is ignored.

*Precedent: language servers' `--hostPID` and VS Code's `--parentPid`, which watch the process that started them and exit with it.*

## Development script

### CLI-46: A headless browser no tool drives opens no development channel

- **Given** a page served in development, whose script keeps the development channel open
- **When** a headless browser that no one drives renders it (`chromium --headless --virtual-time-budget=1000 --dump-dom`, whose user agent says `HeadlessChrome` and whose `navigator.webdriver` is false)
- **Then** the script does not open the channel, so nothing keeps the browser's capture from finishing; a browser whose user agent is an ordinary one opens it, and one driven by a tool (Playwright, `navigator.webdriver` true) still gets it (SSR-285), and connecting after `load` is not enough, since Chromium's virtual time waits for any open request.
