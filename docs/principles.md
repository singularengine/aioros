# Principles

The rules every design decision in Aioros follows. When two options look equally good, these decide. The [architecture](architecture.md) shows them applied.

## Design

**Organize, don't invent.** Every piece the framework needs already exists somewhere. Before deciding, read how Next.js, React Router, Leptos, Phoenix LiveView, ASP.NET Core or Avalonia solved the problem, in their source and tests, and adapt the best answer to XAML and MVVM. An open question is never settled by assumption.

**Everything is composition.** The web, the desktop and later targets are compositions of small capabilities, such as live updates, navigation and hot reload, not modes of one engine. Each target brings the systems that run them.

**Specialize at build time.** Interfaces and reflection cost time on every request. Source generators resolve the routes, the accessors and the composition into concrete code, which also makes Native AOT possible.

**Avalonia is a dependency, not a coupling.** Only one engine project references it, and everything else talks to Aioros's own abstractions. The XAML dialect is Avalonia's, and the subset Aioros supports is documented as its own contract.

**The router stands alone.** Routing depends on no UI and no host. The web and the desktop each adapt it.

**Streaming, never locks.** Components coordinate through an event bus and channels. Each piece of state has one owner, and readers get immutable snapshots.

## Experience

**Installed and productive in a minute.** `dotnet new aioros` gives a working app with hot reload and nothing else to set up. A one-file app works too.

**Hot reload from the first day.** A saved XAML file reaches the browser and the native app while keeping the page's state.

**No JavaScript in front of the developer.** You write XAML and C# on every target. What the browser needs is one small script that belongs to the framework.

**A thin layer over an ordinary app.** An app written with good MVVM practice should port mostly unchanged. Aioros appears in the XAML and in the composition, never in view models or service contracts.

**Flag what depends on the platform.** An app runs as it is on every target, and each common need gets one default that behaves the same everywhere. Where the right answer depends on the platform and on the app, such as a real file path or a huge upload, the framework gives you `OnPlatform` to branch rather than hiding the difference behind an emulation. Some needs are not worth solving on the web at all, and for those the answer is the native app.

## Quality

**Verify against an external reference.** A control is correct when it matches Avalonia's own render, never when it matches our previous output. The compatibility bench renders each page natively and on the web and compares them.

**Every Avalonia control on the web.** The goal is the whole control set, using native HTML where the web has an equivalent. What is guaranteed is what the project controls: the base controls under Fluent, Aioros.Cosmo and Aioros.Pictor. Third-party themes and libraries are best effort.

**Fast by default, and measured.** Pages compile to plans, the request path never touches Avalonia or reflection, and bytes are written without copies. Performance is compared against other stacks on the same app. See [Benchmarks](benchmarks.md).

**Modern C# on the hot path.** C# 14 on .NET 10, with `ValueTask`, `Span<T>` and generated code where they matter.

**Rules are enforced, not remembered.** Architecture tests and analyzers keep people and agents on the architecture. Each rule is checked against a fixture that breaks it on purpose, so no rule can pass by finding nothing.

**English in the repository.** Code, names, documents and commit messages are written in English.
