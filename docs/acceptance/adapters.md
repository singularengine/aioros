# MVVM adapters

Optional packages that map an MVVM toolkit's navigation and lifecycle onto Aioros, so a view model written for that toolkit runs on the web and the desktop with as few changes as possible (principle 15). The core gives them one hook, `IModelLifecycle` (`src/Aioros.Abstractions/Pages/IModelLifecycle.cs`): a scoped service that is told of every view model a page creates. Each adapter finds its toolkit's hooks by shape at build time with a small generator of its own, never by reflection, and reports at build time what exists only between native navigations.

`Aioros.ReactiveUI` (`src/Adapters/Aioros.ReactiveUI`, with its generator in `src/Adapters/Aioros.ReactiveUI.Generators`) maps ReactiveUI 26: `IScreen.Router` navigations go through the page's navigator to the route declared for the routable view model's type, the screens of a page hold its routable view model as their current one, and `WhenActivated` runs once per page (per request on the web, per page shown on the desktop). Prism, Uno.Extensions and MAUI Shell are mapped in [docs/architecture.md](../architecture.md#decisions) only (see "Thin layer over Avalonia").

9 criteria. Each has an identifier and exactly one test.

## Model lifecycle

### MVVM-01: Every created model is reported once before the render

- **Given** a page with a layout whose services hold an `IModelLifecycle`
- **When** the page is requested
- **Then** `CreatedAsync` is called once for each model the page creates, the layout's first, with the page's request, and every call has completed before the page renders
- **When** a command's revalidation creates the page's model again from fresh data while the layout's model receives its data
- **Then** only the new page model is reported; a page whose services hold no `IModelLifecycle` loads as before.

## Reactive command

### REACTIVE-01: Reactive commands are awaited until they end

- **Given** ReactiveUI's own `ReactiveCommand` (the real package), one made with `CreateFromTask` and one with `CreateFromObservable` over a routing state's `Navigate`
- **When** each is run for a post
- **Then** the run ends only when the task has ended or the navigation has been made, and a command whose task faults fails the run with that exception (ENGINE-42 and ENGINE-44 with the real package).

## Reactive navigation

### REACTIVE-02: Router navigations go to the mapped route

- **Given** `AddReactiveRoutes` mapping `SearchViewModel` to `/search` and `AlbumViewModel` to `album => $"/albums/{album.Id}"`, and a page whose view model received the page's `IScreen` from DI
- **When** a command executes `HostScreen.Router.Navigate` with an `AlbumViewModel` of id 7
- **Then** the page's navigator records `/albums/7` (a `303` after the post on the web), and when it executes `NavigateAndReset` with a `SearchViewModel`, then it records `/search`.

### REACTIVE-03: Unmapped view models fail the command

- **Given** the same routes
- **When** a command navigates to a routable view model whose type is not mapped
- **Then** the command fails with an `InvalidOperationException` that names the type and `Map`, and no navigation is recorded.

### REACTIVE-05: Screens hold the page view model

- **Given** a layout view model that is an `IScreen` with its own `RoutingState` and a page view model that is routable
- **When** the page loads
- **Then** the layout's `Router.NavigationStack` holds the page's view model alone, so `CurrentViewModel` is the page's as `RoutedViewHost` would show it, and no navigation is recorded; when the layout's own `Router.Navigate` then executes with a mapped view model, the navigation is recorded as one from the page's `IScreen`.

## Reactive desktop

### REACTIVE-04: Commands move the desktop page stack

- **Given** the desktop target showing a page whose view model is routable and activatable, with a button bound to a `ReactiveCommand` that executes `HostScreen.Router.Navigate` with an `AlbumViewModel` of id 7
- **When** the button is clicked
- **Then** the desktop navigator moves its page stack to `/albums/7` and shows that page, and the view model of the page it left was activated once while shown and has been deactivated (activation per page shown on the desktop).

## Reactive activation

### REACTIVE-06: View models activate once per page

- **Given** a page view model that is an `IActivatableViewModel` with a `WhenActivated` block
- **When** the page loads
- **Then** the block runs once, before the page renders
- **When** the scope the page's models came from ends (after the response on the web, when the page is left on the desktop)
- **Then** what the block registered is disposed
- **When** the page loads again in a new scope
- **Then** the block runs again.

## Setup

### REACTIVE-07: View models are found by their shape

- **Given** an app's classes
- **When** the app is built with the adapter's generator
- **Then** it emits `Aioros.Generated.AiorosReactiveViewModels.All`, which handles by type, without reflection, every class that has an `Activator` of type `ViewModelActivator`, a `Router` of type `RoutingState`, or implements `IRoutableViewModel`, a derived class before its base, and handles no other class; and an `AddReactiveRoutes(Action<ReactiveRoutes>)` extension on `IServiceCollection` that registers the adapter with that table; a project that does not reference ReactiveUI gets nothing.

## Plain

### REACTIVE-08: Interactions only a native view handles are reported

- **Given** a class with an `Interaction<TInput, TOutput>` property or field that is not private (a view can only handle what it can see)
- **When** it is built with the adapter's generator
- **Then** the warning `AIOROSRX001` is reported at the member, naming it and its class and saying that only a native view handles it, unless the class itself registers a handler on that member (`RegisterHandler`), which then runs wherever the view model runs.
