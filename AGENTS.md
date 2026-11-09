# Working on Aioros

Guidance for every agent and person working on Aioros. The rules below are enforced by the architecture tests and the analyzers wherever they can be; the rest is the workflow that keeps the code on its architecture.

## Before you change anything

1. Read [docs/principles.md](docs/principles.md) and [docs/architecture.md](docs/architecture.md).
2. Check the [roadmap](docs/roadmap.md) to see what the work belongs to.
3. Build and test. Both must be green before and after your change. The repository builds with the SDK its CI uses, pinned in `global.json`; `eng/install-sdk.sh` installs it into `.dotnet/` (a git worktree needs it too). The framework's browser scripts are readable files under `src/Targets/Aioros.Targets.Ssr/Scripts/src`, minified with esbuild when that project builds; `eng/install-scripts.sh` installs esbuild (Node 22 or later) from its lockfile, once per clone or worktree.
4. Do not reinvent and do not assume. When a design question comes up (routing precedence, layouts, data loading, hydration, hot reload), find how the reference projects solved it, in their source and tests: Next.js, React Router, Rust frameworks (Leptos, Dioxus, `matchit`), and our own Avala. Note the precedent next to the decision in the [architecture](docs/architecture.md#decisions) or in the acceptance criteria.

```
dotnet build Aioros.slnx
dotnet test --solution Aioros.slnx
```

## Writing tests

Tests come before the code they verify. Every test follows these steps.

### 1. Write the acceptance criteria first

Add the criteria to the area's file in [docs/acceptance](docs/acceptance) before writing any test. Each criterion:

- is a section headed `### AREA-NN: short title`, whose identifier is never reused;
- describes one observable behavior as a list of **Given**, **When** and **Then** lines, under a heading for its topic;
- names the expected outcome precisely: the returned value, the event, the error or the new state;
- covers the rejected paths as well as the successful one.

### 2. Check for duplicates

Before writing a test, verify that no criterion and no test already covers the behavior: search the acceptance files and the test projects for the operation and the asserted outcome. Extend an existing test instead of adding a second one; merge overlapping tests in the same change.

### 3. Write the tests

- **One test per criterion.** The test carries its criterion: `[Trait("AC", "ROUTING-01")]`. The architecture tests check that every criterion has exactly one test and that no test names a criterion that does not exist, so an untested criterion or a duplicate fails the build.
- The test name states the behavior in PascalCase: `ParametersAreCapturedAndUnescaped`, with the `Async` suffix for asynchronous tests.
- **Unit tests by default.** Integration tests only when a behavior cannot be verified otherwise (a real HTTP host, the file system, a browser), and they still carry their criterion.
- Write the test first and watch it fail, then write the code that makes it pass.
- No sleeps, no delays, no waits on time: they do not compile in tests either. Await the event or signal that states the thing happened, and drive behavior that is genuinely about time with a fake `TimeProvider`.
- Prove that a new rule or guard can fail: break it on purpose, watch the test fail, then restore it.

## Adding or completing a control

Every Avalonia control is tracked in [docs/controls.md](docs/controls.md). To complete one:

1. Read how Avalonia builds it (its Fluent template and the ControlCatalog page) and how the web does the same thing natively (MDN, the HTML specification). Prefer a native element: `<input>`, `<select>`, `<progress>`, `<details>`, `<dialog>`, popovers. Only draw it with `div`s when the web has no equivalent.
2. Write its criteria in `docs/acceptance/controls-<family>.md` (`TEXT-`, `BUTTONS-`, `RANGES-`, `ITEMS-`, `NAVIGATION-`, `MEDIA-`, `LAYOUT-`), after checking that no criterion covers it already.
3. Test it in `tests/Aioros.Avalonia.Tests/Controls/<Family>ControlsTests.cs` with `ControlHarness` (`fixture.HtmlAsync`, `fixture.CompileAsync`, `fixture.PostAsync`), one test per criterion, and watch it fail.
4. Write it as a static method in a writer class under `src/Adapters/Aioros.Avalonia/Html/Writers/`, registered in `Html/Families/<Family>Family.cs`. Order matters: a derived control comes before its base (`ToggleSwitch` before `ToggleButton` before `Button`). A control that posts a value calls `context.Field(control)` where its field id goes and adds a binder to the family's `Binders`.
5. Show it in the Control Gallery (`samples/Gallery/Gallery/Pages/controls/<family>/Page.axaml`) in its main states.
6. Mark it `done` in `docs/controls.md` with its criteria. The coverage test fails on a `done` row without criteria.

## Architecture

Enforced by `tests/Aioros.ArchitectureTests` and the compiler. Do not work around a rule; if one blocks a legitimate change, raise it.

- **Roles and references.** Every project has a role in `tests/Aioros.ArchitectureTests/Workspaces/ProjectMaps.cs`, and the role fixes what it may reference (see the layout in [docs/architecture.md](docs/architecture.md#layout)). A new project gets its role in the same change.
- **Avalonia only in the adapter**, the native target, the platform profile and `Aioros.Markup` (the `Web` attached properties libraries may reference); ASP.NET Core only in web targets and tools; no reflection on the request path.
- **Composition over inheritance.** Every class and record class is `sealed` or `static`.
- **Streaming, never locks.** Coordinate through `IEventBus` or channels. No `lock`, no coordination primitive, no concurrent collection in `src/`: one owner per state, readers get immutable snapshots published with `ImmutableInterlocked` or `Volatile`.
- **Specialize at build time.** Prefer a source generator to runtime discovery, reflection or a runtime cache.
- **Screaming architecture.** Folders and namespaces name what the code does, never technical layers.

## Code

- Modern C# 14 on .NET 10. The smallest code that states the behavior clearly.
- No comments. XML documentation is allowed on the public API only.
- No type longer than 600 lines.
- Nothing blocks a thread. Nothing waits on time to coordinate: await the event or signal that states the thing happened. Behavior that is genuinely about time (retries, expirations) waits through an injected `TimeProvider` only (`Task.Delay(delay, timeProvider, cancellationToken)` or `TimeProvider.CreateTimer`), so tests drive it with `FakeTimeProvider`.
- Warnings are errors.

## Documentation

- Everything in the repository is written in English.
- Update the principles, the architecture and the roadmap in the same change when a decision changes.
- **Branding.** Product surfaces (READMEs, Galleries, demos, page metadata, package and template descriptions) lead with the ecosystem's brands: Aioros ("The XAML framework for web and native apps"), Aioros.Cosmo, Aioros.Pictor, Aioros.Graphs. Avalonia is named once, as a compatibility statement where it helps a developer; shadcn/ui, Recharts and d3 appear as the reference on fidelity and compare pages and in the credits; comparisons say "native" or "desktop render". Internal technical documents, code and type names keep their names.
