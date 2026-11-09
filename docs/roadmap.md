# Roadmap

Where Aioros stands and where it is going. Aioros is in public preview: it works and is tested, its APIs can still change, and it is not ready for production yet.

**The goal:** install Aioros the way you install Next.js, start working at once with hot reload, and have every control ready and tested.

## What exists today

### Web

- [x] File-based routes, layouts, loaders and typed route helpers
- [x] Server rendering from precompiled plans, verified against the real XAML render
- [x] Hot reload of XAML and C# that keeps what you typed, the scroll and the focus
- [x] Commands over `fetch` answered in place, partial navigation and live updates
- [x] Declared interactions in one small generic script, such as drag and drop with a keyboard alternative
- [x] Forms that survive a dropped connection, with drafts per signed-in user
- [x] List items posted by key, so a post never lands on another item
- [x] Every Avalonia control written for the web, tracked in [Controls](controls.md)
- [x] Sign-in and access rules for pages and server functions

### Tooling

- [x] Preview packages on NuGet
- [x] The `aioros` command line and `dotnet new aioros`
- [x] Source generators and analyzers that report problems at build time
- [x] Native AOT and ReadyToRun builds
- [x] A compatibility bench that compares each page with its native render

### Libraries

- [x] [Aioros.Cosmo](https://github.com/singularengine/aioros-cosmo): UI components and themes
- [x] [Aioros.Pictor](https://github.com/singularengine/aioros-pictor): charts in pure XAML
- [x] [Aioros.Graphs](https://github.com/singularengine/aioros-graphs): the chart engine

## In progress

### Desktop

The same app runs as a native window with `dotnet run -- --desktop`, with navigation, commands, themes and hot reload.

- [ ] Data over RPC for a desktop client of a remote server
- [ ] Installers and auto-update
- [ ] Testing on Windows and macOS

## What comes next

- **Design time.** See a view with sample data on several devices without running the app, for people and for agents.
- **Android and iOS,** after the public launch: touch and gestures, device APIs, testing on real devices and store publishing.
- **Public APIs upstream,** proposed to the UI toolkit for the internals Aioros reaches into today.

Every stage ends with performance measured against other stacks on the same app, with the results in [Benchmarks](benchmarks.md).
