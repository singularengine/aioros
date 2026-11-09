<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/brand/aioros-logo-dark-bg.svg">
  <img src="docs/brand/aioros-logo-light-bg.svg" alt="Aioros" width="320">
</picture>

**The XAML framework for web and native apps.**

Write XAML and C#. Serve real HTML on the web. Run natively on the desktop.

[Why](#why-aioros-exists) · [Getting started](docs/getting-started.md) · [Concepts](docs/concepts.md) · [Architecture](docs/architecture.md) · [Numbers](#numbers)

</div>

> **Public preview.** APIs can change between versions and Aioros is not ready for production yet. Packages are published as `0.1.0-preview.N`.

## Why Aioros exists

I was a Microsoft MVP in Windows Development, and I loved WPF and Silverlight. When Silverlight was discontinued, what stayed with me was the wish to write XAML for the web. The options I tried over the years were never what I was looking for, and they tended to end up as a niche.

I wanted to build apps at the level of Next.js and its peers, without JavaScript. Not because JavaScript is wrong, but because the mental model of .NET desktop developers, from WPF to MAUI and Xamarin, is XAML. It would help a lot to bring that model to the web, for anyone who feels like using it.

The current XAML-on-the-web options rely on WebAssembly, and in my experience their performance leaves a lot to be desired. Aioros sends the browser HTML and CSS instead.

In my opinion, the JavaScript ecosystem, as widely adopted as it is, is also the most fragmented. What I like about XAML and C# is exactly their stability and predictability. They are not better; they are an alternative way of writing web apps.

Aioros is compatible with Avalonia's XAML, so the same app also runs natively on the desktop, with no Electron or other wrapper. That is a real boost in performance, and it opens up new ways to use an app with less friction. As AI takes on a larger part of how software gets built and used, that lower friction leaves more room for immersive experiences.

## What you get

- **One mental model.** Layout, styles, themes, animation, states, validation and data binding live in XAML and C#, with plain MVVM view models.
- **Real HTML on the web.** Pages are server-rendered to HTML and CSS, with about 10 KB of JavaScript on a typical first load. No runtime is downloaded.
- **Native on the desktop.** The same pages, view models and services run in a native window.
- **The Next.js developer experience.** File-based routes, layouts, loaders and hot reload that keeps what you typed, the scroll and the focus.
- **Server-driven interactivity.** Commands post with `fetch` and are answered in place, with live updates and partial navigation.
- **Checked at build time.** Every page compiles to a render plan or the build fails, and analyzers catch what a server would wrongly share between users.

## Getting started

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet new install Aioros.Templates@0.1.0-preview.1
dotnet new aioros -n MyApp
cd MyApp
dotnet watch --project MyApp
```

Open `http://localhost:3000`, then save a `.axaml` or a `.cs` file and the tab updates in place. To run the same app as a native window:

```bash
dotnet run --project MyApp -- --desktop
```

A page is XAML with its view model, at the route its folder names:

```xml
<!-- Pages/Page.axaml is the route "/" -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:m="using:MyApp.Models"
             x:DataType="m:HomeModel">
  <StackPanel Spacing="12">
    <TextBlock Classes="h1" Text="Welcome to Aioros" />
    <Button Classes="accent" Content="Count" Command="{Binding CountCommand}" />
    <TextBlock Text="{Binding Message}" />
  </StackPanel>
</UserControl>
```

Routes follow the Next.js App Router: `Pages/issues/[id]/Page.axaml` is `/issues/{id}`, `Layout.axaml` wraps every page below it, and `(group)` folders do not change the URL. A page that needs data adds a loader beside it, `Page.Loader.cs`.

The CLI covers the rest (`dotnet tool install -g Aioros.Cli --prerelease`): `aioros new`, `dev`, `build`, `start` and `routes`. `aioros build` publishes and precompiles the pages, so production serves them without starting the XAML engine.

## How it works

The first time a page is compiled, Aioros traces how the real XAML renders and turns the result into a **render plan**: the static HTML and CSS, with slots for what the view model binds. Serving a request then fills the slots, without running the UI framework. Plans are verified against the real render, and a plan never freezes a bound value. See [Traced Render Compilation](docs/trc.md).

On the desktop nothing is translated: the XAML runs natively.

## Numbers

The same Linear-style SaaS app, built with each stack in its best official configuration. Requests per second on `/issues`, plus what a first load costs:

| | Aioros | Blazor (static SSR) | Next.js (cached) | Go + htmx | Rust (Leptos) |
|---|---|---|---|---|---|
| Requests/s | **31,917** | 2,030 | 2,254 | 9,570 | 5,370 |
| JavaScript on first load | 9.7 KB | 46.8 KB | 160.3 KB | 20.3 KB | 7.3 KB |
| Save, click to confirmation | 51 ms | 39 ms | 78 ms | 34 ms | 19 ms |
| Memory after load | 134 MB | 172 MB | 881 MB | 15 MB | 23 MB |
| Cold start | 461 ms | 540 ms | 597 ms | 19 ms | 25 ms |

A save is slower than most, and Go and Rust use far less memory and start far faster. With Native AOT, Aioros starts in about 120 ms and stays at 77 MB after the load test. Numbers are from one run on a shared machine, on 2026-10-19; read them as trends. Method and every run are in [docs/benchmarks.md](docs/benchmarks.md).

## The ecosystem

| Project | What it is |
|---|---|
| **Aioros** | The framework |
| [Aioros.Cosmo](https://github.com/singularengine/aioros-cosmo) | UI components with variants, design tokens and light and dark themes |
| [Aioros.Pictor](https://github.com/singularengine/aioros-pictor) | Charts in pure XAML, on the web and natively |
| [Aioros.Graphs](https://github.com/singularengine/aioros-graphs) | The chart engine in pure C# |

Each control's state on the web is tracked in [docs/controls.md](docs/controls.md).

## Status

| Target | State |
|---|---|
| Web: server rendering | Done |
| Web: interactivity | Done |
| Desktop | In progress |
| Android and iOS | After the public launch |

What is guaranteed on the web is the framework's base controls, Aioros.Cosmo and Aioros.Pictor. Third-party themes and control libraries work on a best-effort basis. See the [roadmap](docs/roadmap.md).

## Samples

```bash
dotnet run --project samples/Gallery/Gallery    # every control
dotnet run --project samples/Lumen/Lumen        # a SaaS app
dotnet run --project samples/Showcase/Showcase  # finance, CRM, music, a kanban and a store
```

## Building from source

```bash
dotnet build Aioros.slnx
dotnet test --solution Aioros.slnx
```

The repository pins its SDK in `global.json`, and `eng/install-sdk.sh` installs it. See [CONTRIBUTING.md](CONTRIBUTING.md) for how changes are made.

## Documentation

| Document | What it holds |
|---|---|
| [Getting started](docs/getting-started.md) | From nothing to a running page |
| [Concepts](docs/concepts.md) | Pages, loaders, commands and styles |
| [Principles](docs/principles.md) | The rules every decision follows |
| [Architecture](docs/architecture.md) | The system, its decisions and their precedents |
| [Traced Render Compilation](docs/trc.md) | How pages compile to plans |
| [Controls](docs/controls.md) | Every control and how it is written on the web |
| [Acceptance criteria](docs/acceptance) | What each area must do, each with exactly one test |
| [Benchmarks](docs/benchmarks.md) | The method and the results |
| [Parity](docs/parity.md) | Every XAML construct measured against its native render |
| [Roadmap](docs/roadmap.md) | Phases, deliverables and what comes next |

## Security

Please report vulnerabilities privately, as described in [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE). Third-party notices are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
