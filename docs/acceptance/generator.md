# Source generator

The page registry generator (`src/Aioros.Generators/Pages`): it reads the project's `.axaml` files and emits, at build time, every route with its page, layouts and view-model factories, so the app discovers nothing at runtime.

86 criteria. Each has an identifier and exactly one test.

## Shell model

### GENERATOR-01: Pages are registered with their route source and model

- **Given** `Pages/issues/[id]/Page.axaml` whose root declares `x:DataType` for a view model
- **When** the project is built
- **Then** the registry holds the route `/issues/{id}`, the source `Pages/issues/[id]/Page.axaml` and a factory that builds that view model.

### GENERATOR-02: Models take their services from the provider

- **Given** a view model whose constructor takes services
- **When** its factory runs
- **Then** each argument comes from the service provider, and a view model registered in the provider is returned as is.

### GENERATOR-03: Layouts are listed outermost first

- **Given** `Layout.axaml` files around a page
- **When** the project is built
- **Then** the page's layouts are listed outermost first, each with its own view-model factory, or none when the layout declares no `x:DataType`.

### GENERATOR-04: Live pages are marked

- **Given** a page whose root sets `a:Web.Live="True"`
- **When** the project is built
- **Then** the page is registered as live.

### GENERATOR-05: Unknown models are build errors

- **Given** a page whose `x:DataType` names a type that does not exist
- **When** the project is built
- **Then** the build reports error `AIOROS001` at that file.

### GENERATOR-06: Conflicting routes are build errors

- **Given** two pages that resolve to the same route
- **When** the project is built
- **Then** the build reports error `AIOROS002` naming both files.

### GENERATOR-07: The application type is found

- **Given** a project with a class deriving from `Avalonia.Application`
- **When** the project is built
- **Then** the registry names it as the application type
- **When** none
- **Then** the registry names no application type.

### GENERATOR-68: Members reactive UI generates are named by its rules

- **Given** a view model reached from the XAML with fields marked ReactiveUI.SourceGenerators' `[Reactive]` (`_first`, `m_second`, `__third`, `fourth`, one with `SetModifier = AccessModifier.Protected`) and methods marked `[ReactiveCommand]` (`Save`, `OnSubmit`, `LoadAsync` returning `Task`, `PingAsync` returning `ValueTask`)
- **When** the app is built
- **Then**
  - `AiorosAccessors.All` holds getters for the properties that generator writes, named by its rules (`First`, `Second`, `Third`, `Fourth`, a setter for each except the protected one; `SaveCommand`, `OnSubmitCommand`, `LoadCommand`, `PingAsyncCommand`: the method's name, with `Async` dropped only from a method returning a `Task`, and `Command` appended), since one generator cannot see another's output
  - ReactiveUI.Fody's `[Reactive]` marks a public auto-property, which is read as written.

*Precedent: ReactiveUI.SourceGenerators 4.2.0's README ("Usage Reactive property", "Usage ReactiveCommand ... Note: the Async suffix is removed"), confirmed against its output.*

### GENERATOR-118: Read only lists indexed in XAML get index accessors

- **Given** an app type that is an `IReadOnlyList<T>` but not an `IList` (a ranking of its own) and XAML that indexes it (`{Binding Ranking[0].Name}`)
- **When** the app is built
- **Then** `AiorosAccessors.All` holds a getter named `[0]` on that type that reads its element without reflection and reads null when the index is out of range.

## Main app

### GENERATOR-08: The host entry point is generated when the host is referenced

- **Given** a project that references the Aioros host
- **When** it is built
- **Then** it gets `AiorosApplication.CreateBuilder(args)`, which passes the host the registry's pages, application type, assembly and project directory
- **When** no host
- **Then** nothing is added.

## Aioros host

### GENERATOR-09: Duplicated files are listed once

- **Given** the same XAML file passed twice, as `dotnet watch` does
- **When** the project is built
- **Then** the registry lists its page once and the build reports nothing.

### GENERATOR-10: The not found page is registered

- **Given** `Pages/NotFound.axaml`
- **When** the project is built
- **Then** the registry exposes it as the not-found page with its layouts and view-model factory, and the entry point passes it to the host
- **When** none
- **Then** the registry has no not-found page.

### GENERATOR-11: The error page is registered

- **Given** `Pages/Error.axaml`
- **When** the project is built
- **Then** the registry exposes it as the error page and the entry point passes it to the host
- **When** none
- **Then** the registry has no error page.

### GENERATOR-12: Typed routes build URLs

- **Given** the routes `/`, `/issues`, `/issues/{id}`, `/issues/{id:int}/edit` and `/docs/{*slug}`
- **When** the project is built
- **Then** `Routes` has `Home`, `Issues`, `IssuesById(string id)`, `IssuesEditById(int id)` and `DocsBySlug(string slug)`, which build the URLs with each value escaped and the catch-all escaped per segment.

### GENERATOR-13: Sources are embedded on request

- **Given** a project with `AiorosEmbedSources` set to `true`
- **When** it is built
- **Then** `AiorosSources.Text(path)` returns the XAML of each page and layout by its project-relative path (quotes and line breaks kept) and `null` for an unknown path; without the property no sources class is generated.

### GENERATOR-55: The baked server origin is passed to the host

- **Given** a project that references the Aioros host and sets the MSBuild property `AiorosServer` to an origin
- **When** it is built
- **Then**
  - `AiorosApplication.CreateBuilder(args)` passes the host that origin as `AiorosApp.Server`; without the property, then `AiorosApp.Server` is not set.
  - The same holds for the installed app's URL scheme, `AiorosScheme`, passed as `AiorosApp.Scheme` (INSTALL-01).

## Plain model

### GENERATOR-14: Page loaders feed the view models constructor

- **Given** `Pages/issues/[id]/Page.axaml` and, beside it, `Page.Loader.cs` declaring a class that implements `ILoader<IssueData>`
- **When** the project is built
- **Then** the page's definition carries a loader that builds the loader class (from the provider when it is registered there, otherwise with its constructor's services from the provider), calls its `LoadAsync` with the request and returns its data, and a factory that passes that data to the view model's constructor parameter of type `IssueData` and every other parameter from the provider; the view model is never taken from the provider, which cannot know the data.

*Precedent: SvelteKit's `+page.server.ts` beside `+page.svelte`, Uno.Extensions `DataViewMap<TView, TViewModel, TData>`.*

### GENERATOR-15: Page and layout loaders are told apart by file name

- **Given** a folder with both `Page.axaml` and `Layout.axaml`, and beside them `Page.Loader.cs` and `Layout.Loader.cs`
- **When** the project is built
- **Then** each loader goes to the file it is named after: the page gets the page loader, the layout (for this page and every page below the folder) gets the layout loader, and a layout without a loader file has none.

*Precedent: SvelteKit tells `+page.server.ts` from `+layout.server.ts` by file name; Remix gives each route module, layout or page, its own `loader`.*

### GENERATOR-24: Ambiguous or wrong receive methods are build errors

- **Given** a view model whose method named `Receive` takes the loader's data type but cannot take new data (it is not public, is static, returns a value, or takes more parameters), or that has two candidates (a public `Receive(TData)` and an explicit `IRecipient<TData>.Receive`)
- **When** the project is built
- **Then** the build reports error `AIOROS008` at the XAML file, naming the view model, the data type and the reason, so new data is never pushed into the wrong method or silently dropped.

## Home model

### GENERATOR-16: Pages without loaders are built as before

- **Given** a page and a layout without a loader file
- **When** the project is built
- **Then** their definitions carry no loader and their factories build the view models as before (the provider's instance first, else the public constructor with the most parameters, each from the provider).

### GENERATOR-17: View models asking for data no loader provides are build errors

- **Given** a page or layout whose view model's constructor takes a type that a loader of the project returns, but whose own loader does not return it (or that has no loader)
- **When** the project is built
- **Then** the build reports error `AIOROS004` at its XAML file, naming the view model and the data type.

## Other model

### GENERATOR-18: Loader data the view model does not take is a build error

- **Given** a loader whose data type the view model of its page or layout does not take in its constructor (or whose page declares no view model)
- **When** the project is built
- **Then** the build reports error `AIOROS005` at the XAML file, naming the loader and its data type.

### GENERATOR-19: Loaders away from their page are build errors

- **Given** a class implementing `ILoader<T>` in a file that is not named after a page or layout beside it (`Page.Loader.cs` next to `Page.axaml`, `Layout.Loader.cs` next to `Layout.axaml`), a class with a public `LoadAsync` method in a `.Loader.cs` file that is not named after one, or a second loader for the same file
- **When** the project is built
- **Then** the build reports error `AIOROS006` at the class, so a loader is never silently ignored.

### GENERATOR-38: Loaders without data need no view model

- **Given** a class implementing the non-generic `ILoader` in `Page.Loader.cs` beside a page with no `x:DataType`, and another beside a page whose view model takes no loader data
- **When** the app is built
- **Then** neither `AIOROS004` nor `AIOROS005` is reported, each page's definition has a loader that runs the class and answers its result with no data, and the view model is created as without a loader (from DI, else its constructor); its data is written for the installed app (`/_data`, SSR-56) as `null` and read back as no data, and the page's data contract is the same as without the loader.

*Precedent: a Remix loader that only returns `redirect`.*

## Helpers

### GENERATOR-20: Loader results reach the runtime unchanged

- **Given** a loader that returns `LoaderResult.NotFound`, `LoaderResult.Redirect(path)`, or its data with a title and metadata
- **When** the generated definition runs it
- **Then** the result it hands the runtime carries the not found, the redirect, the title and the metadata unchanged with the data, with no reflection.

## Issue loader

### GENERATOR-21: Deferred loader data is a build error

- **Given** a loader whose data type is `IQueryable`, `IQueryable<T>` or `IAsyncEnumerable<T>` (or implements one of them, as `DbSet<T>` does), or is a type with a public property or field, a record's parameters included, of such a type (one level deep)
- **When** the project is built
- **Then** the build reports error `AIOROS007` at the loader class, naming the loader, its data type and the property, and saying that the data must be materialized (a list or array) because the loader's scope is disposed when it returns and because the data is sent to the installed app.

### GENERATOR-22: Materialized loader data builds cleanly

- **Given** loaders whose data is a `List<T>`, an array, or a record whose properties are lists, arrays, strings and numbers
- **When** the project is built
- **Then** no `AIOROS007` is reported.

### GENERATOR-29: Loader data the installed app cannot read is a build error

- **Given** a loader whose data is or holds, at any depth, an `object`, an interface or abstract class other than the supported collections without System.Text.Json's `[JsonDerivedType]` attributes (the message names `[JsonPolymorphic]` and `[JsonDerivedType]`), a delegate, a framework type that is not data (`Stream`, `Uri`), a dictionary whose keys are not strings, a type without a public constructor whose parameters all match its public properties, or a type the generated code cannot reach (a private nested type)
- **When** the project is built
- **Then** the build reports error `AIOROS011` at the loader class, naming the loader, its data type, the member's path and the reason; data already reported by `AIOROS007` is not reported again.

## Page receive generator

### GENERATOR-23: View models that receive data get it pushed

- **Given** a loader whose page's view model has a public instance method `Receive(TData)` returning `void`, taking exactly the loader's data type (written by hand, or implementing CommunityToolkit's `IRecipient<TData>`, also explicitly)
- **When** the project is built
- **Then** the loader's definition carries a `Receive` that calls that method on a view model with new data, directly, with no reflection; a view model without such a method gets none, so it is created again from new data.

*Precedent: CommunityToolkit.Mvvm `IRecipient<TMessage>.Receive`, the idiom view models already use to take pushed values; SvelteKit's `data` prop updated in place.*

## Setup

### GENERATOR-25: Registrations of unusable types are reported

- **Given** `AddServerFunction<TService, TImplementation>` naming a class, a generic interface, or an interface with a generic method
- **When** the project is built
- **Then** the build reports error `AIOROS009` at the call, naming the type and the reason, and generates no client or group for it.

### GENERATOR-26: Groups that are not constant or collide are reported

- **Given** a registration whose group is not a constant string, two registrations giving the same interface different groups, or two interfaces with the same group
- **When** the project is built
- **Then** the build reports error `AIOROS009` at the call, naming the interface and the reason, so a route never changes or collides silently.

### GENERATOR-31: Static mutable state in view models is reported

- **Given** a page's or layout's view model (or a base class of it declared in the app) with a static field that is not `readonly`, a static property with a setter, or a `static readonly` field whose type is a mutable collection (a `List<T>`, a dictionary, a set, an `ObservableCollection<T>`, a concurrent collection, or a mutable collection interface)
- **When** the project is built
- **Then** the analyzer reports warning `AIOROS013` at the member, naming the view model and the member and saying that static state is shared by every user on a server; constants, `static readonly` fields of other types, arrays, immutable, frozen and read-only collections, and instance members are not reported.

### GENERATOR-45: Keys that cannot be written are reported

- **Given** a server implementation whose `[Reads]` or `[Invalidates]` key names a placeholder that is no parameter of the method (`issue:{nope}`), a member that the parameter's type does not have (`{changes.Nope}`), a placeholder left open (`issue:{id`) or an empty key
- **When** the app is built
- **Then** `AIOROS019` reports it at the attribute with the key and the reason, and no code is generated for the group; valid keys produce no diagnostic.

### GENERATOR-50: Memoized functions run through the loads memo

- **Given** a server implementation whose method is marked `[Memo]`
- **When** the app is built
- **Then**
  - Its in-process caller runs the call through the memo of the loader's load, keyed by the group, the method and the arguments, so equal calls share one run and one result, and outside a load (a command) it calls the implementation directly
  - `[Memo]` on a method without a result, or whose result is an array, a mutable collection or a type with a public setter or a public writable field, is warning `AIOROS023` at the attribute naming the type and why, since a memoized result is handed to every loader that asked.

*Precedent: React `cache` (opt-in per function, one memo per server request).*

### GENERATOR-53: Cached functions call through the data cache

- **Given** a server implementation whose methods declare `[Cached(30)]` (with `[Reads]`) and `[Cached(30, Shared = true)]`
- **When** the app is built
- **Then**
  - Its in-process caller calls through the data cache with the function's name, its arguments, its lifetime, whether it is shared and its read keys written from the arguments
  - `AIOROS023` warns about `[Cached]` on a function whose result could be changed, as for `[Memo]`; and `[Cached(Shared = true)]` on a function whose implementation takes the request (`IHttpContextAccessor`, `HttpContext`, `ClaimsPrincipal`, `IPrincipal` or `IServiceProvider` in its constructor) is the error `AIOROS024`, and its caller does not cache.

### GENERATOR-54: Cached loaders carry their lifetime

- **Given** loaders marked `[Cached(60)]`
- **When** the app is built
- **Then** each loader's definition carries a 60 s lifetime; a `[Cached]` loader that reads `PageRequest.User` or `PageRequest.Cookies` (its data depends on the visitor, as Next.js's `cookies` makes a route dynamic), takes the request in its constructor or takes the user (a `ClaimsPrincipal` parameter of its `LoadAsync`, GENERATOR-56) is the error `AIOROS024` and its definition carries no lifetime, so its page is never cached.

### GENERATOR-122: Singleton view models are fixed to scoped

- **Given** the warning `AIOROS012` on a view model registered as a singleton (`AddSingleton`, `TryAddSingleton`, `AddKeyedSingleton` or `TryAddKeyedSingleton`, by a type argument or by `typeof`)
- **When** its code fix is applied
- **Then** the call registers it as scoped instead (`AddScoped`, `TryAddScoped`, `AddKeyedScoped`, `TryAddKeyedScoped`), keeping its type arguments, its arguments and the rest of the file as they were, and the warning is gone.

*Precedent: the .NET analyzers' code fixes (`CA1822` makes a member static), offered at the diagnostic in the IDE and by `dotnet format`.*

## Everything model

### GENERATOR-27: Loader data is written and read by emitted code

- **Given** loaders whose data are records and classes with constructor parameters, `init` and settable properties, nested records, arrays, `List<T>`, `IReadOnlyList<T>`, `ImmutableArray<T>`, `Dictionary<string, T>`, enums, nullable values, strings, numbers, `bool`, `char`, `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan` and nulls
- **When** the project is built
- **Then** each loader's definition carries a `Write` and a `Read` emitted as direct `Utf8JsonWriter` and `JsonElement` calls (no reflection and no `JsonSerializer`), with camel-case names, and reading what was written gives equal data; a computed property (neither a constructor parameter nor settable) is not written.

*Precedent: the configuration binding source generator emits per-type code instead of reflection, as System.Text.Json source generation emits per-type metadata.*

### GENERATOR-28: The data contract hashes the pages data shapes

- **Given** a page with a layout loader and a page loader
- **When** the project is built
- **Then** its definition's `DataContract` is a hash of its layouts' and its own data shapes in order (member names and types, nested types, enum values): the same in another compilation of the same shapes, and another when a nested member is renamed or retyped or the layout's data changes; a page without loaders gets the hash of no data.

*Precedent: the server function contract hash (FUNCTIONS-02).*

## Shared state analyzer

### GENERATOR-30: Singleton view models are reported

- **Given** a type named as a page's or layout's `x:DataType`
- **When** the app registers it as a singleton (`AddSingleton`, `TryAddSingleton`, `AddKeyedSingleton` or `TryAddKeyedSingleton`, by a type argument or by `typeof`)
- **Then**
  - The analyzer reports warning `AIOROS012` at the call, naming the view model and its XAML file and saying that one instance would serve every user and request on a server, so it should be scoped or transient with shared data in a singleton service it takes
  - `AddScoped` and `AddTransient` of the same type, and singletons that are not page or layout view models, are not reported.

*Precedent: Microsoft's DI guidelines (learn.microsoft.com/dotnet/core/extensions/dependency-injection-guidelines: a singleton must be thread-safe and must not hold per-user state; captive dependencies).*

## Helper

### GENERATOR-32: Process wide messengers in view models are reported

Given a page's or layout's view model that reads `WeakReferenceMessenger.Default` or `StrongReferenceMessenger.Default` (CommunityToolkit.Mvvm) or Splat's `Locator.Current` or `AppLocator.Current`; the same members used outside view models are not reported.

*Precedent: the CommunityToolkit.Mvvm messenger documentation (learn.microsoft.com/dotnet/communitytoolkit/mvvm/messenger: inject an `IMessenger` instance, e.g. one per scope, instead of the static `Default`).*

## Board model

### GENERATOR-33: Polymorphic data is written and read as system text JSON does

- **Given** loader data holding an abstract record, an interface or a concrete class marked with System.Text.Json's `[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]` (or without it, so the property is `$type`) and `[JsonDerivedType(typeof(X), "x")]` or `[JsonDerivedType(typeof(Y), 7)]` for its concrete types, with members, lists and nulls of the base type
- **When** the project is built
- **Then**
  - The emitted code writes each value as System.Text.Json writes it: the discriminator property first, then the members of the value's runtime type; a value of a concrete base type without a discriminator; a value of an unlisted derived type as its nearest listed ancestor under `UnknownDerivedTypeHandling = FallBackToNearestAncestor`, or a `NotSupportedException` by default.
  - What System.Text.Json writes is read back as the type the discriminator names, in any property order, and what the code writes System.Text.Json reads; a discriminator that names no listed type, or a missing one on an abstract type, is a `JsonException`; the data contract describes every listed type.
  - A type listed without a discriminator is reported as `AIOROS011`.
  - The attributes are the BCL's, so the data's types reference no Aioros.

*Precedent: System.Text.Json polymorphic serialization (learn.microsoft.com/dotnet/standard/serialization/system-text-json/polymorphism).*

### GENERATOR-34: Generated code for nullable members compiles without warnings

- **Given** loader data with nullable reference members (a nullable record, list, dictionary, array and polymorphic member, and a list of nullable strings)
- **When** the project is built with nullable reference types enabled and warnings as errors
- **Then** the generated code compiles without any warning.

### GENERATOR-51: Types named in XAML and reached from them get typed accessors

- **Given** XAML files that name types in `x:DataType` (on the root, on a `DataTemplate` in an item template or in `DataTemplates`, also as `{x:Type}`) or in a `DataTemplate`'s `DataType`, those types' public instance properties and fields holding other app types, lists, arrays and nullable values, and a property typed as an interface or abstract class the app implements
- **When** the app is built
- **Then**
  - `AiorosAccessors.All` lists, for every concrete type reached from them (through public instance members, the element type of a list or array, and the app's implementations of an interface or abstract type), a typed getter for each public instance property and field, inherited ones included, and a setter with its value type for each property of a class with a public `set` (not `init`); each getter returns what the member holds and each setter assigns it, with no reflection, and the generated entry point hands the list to the host.
  - Framework types reached this way (a `List<T>`, a `string`, a `DateTime`) get their own members but are not followed further.

*Precedent: EF Core's compiled model (`dotnet ef dbcontext optimize` emits a typed getter and setter per property instead of building them at run time) and the configuration binding generator, which walks the type graph from each `Bind<T>`.*

### GENERATOR-52: Members a typed accessor cannot serve keep the runtime delegate

- **Given** a type the generated code cannot reach (a private nested type), and members that cannot be read or written the way a typed accessor would (static members, indexers, by-ref-like values such as `Span<T>`, write-only and `init` properties, a non-public getter or setter, members marked `[Obsolete]`, the elements of a value tuple (whose names exist only at compile time, as in a drop command's `(string Item, string Target, int Index)`), a name declared twice in the type's hierarchy, a setter on a struct)
- **When** the app is built
- **Then** no getter or setter is generated for them, so they keep the runtime delegate, and the generated code compiles without any warning with nullable reference types enabled and warnings as errors.

### GENERATOR-120: Commands that touch no Avalonia are listed

- **Given** pages whose `Command` bindings, from an `x:DataType` or a `DataTemplate`'s `DataType`, name commands that touch no Avalonia: a `[RelayCommand]` method, a method command, an `ICommand` property created with a lambda or a method group whose body sets the model's properties and calls the app's own methods (through an interface or an abstract member too, a service the model is given, judged by every implementation the app declares: Lumen's settings save through its profiles service; a record or primary constructor judged by its type's initializers), the base library or an assembly that does not reference Avalonia, and a command of a referenced type that carries `[AnyThread]` (`Aioros.Markup`) read through a property of the model
- **When** the page registry is generated
- **Then** each page's definition lists those binding paths, as written, in `AnywhereCommands`.

### GENERATOR-121: Commands that may touch Avalonia are not listed

- **Given** pages whose `Command` bindings name a command that creates an Avalonia object (`new SolidColorBrush`), posts to Avalonia's dispatcher (`Dispatcher.UIThread.Post`), calls into a referenced assembly that references Avalonia on a type without `[AnyThread]` (a library's static message box), calls an interface member one of whose implementations in the app touches Avalonia, or one with no implementation in the app, awaits a task it does not start (a `TaskCompletionSource`'s task, or a task held in a field or property: a command that waits on the visitor's answer, which only the engine answers while it waits, "Posts that wait on the user".Cosmo's dialog fixtures, whose posts hung once the plan ran them), or is read through a property of such a type, and bindings the generator cannot follow (an `ElementName`, a `$parent[…]` or `RelativeSource` source, no data type in scope, a member that does not exist)
- **When** the page registry is generated
- **Then** none of those paths is listed in `AnywhereCommands`, and a page without any command lists none.

### GENERATOR-124: Page commands reached from templates are listed with their depth

- **Given** a page whose templates bind commands of the page's own model the way Avalonia writes it, `$parent[UserControl].((vm:BoardModel)DataContext).Clear` (the root element's type in brackets, the page's `x:DataType` in the cast), one inside one template and one inside two nested templates
- **When** the page registry is generated
- **Then** those whose commands touch no Avalonia (GENERATOR-120's rules) are listed in `AnywhereCommands` with the depth of the outer scope the plan reads them, `^1.Clear` and `^2.MoveCommand`; one whose command touches Avalonia, one naming another element in brackets (`$parent[StackPanel]`) and one casting to another type are not listed.

## Aioros navigation registration

### GENERATOR-35: Registered interfaces navigate through the navigator

- **Given** an interface the app declares and registers with `builder.AddNavigation<TInterface>` (or `services.AddNavigation<TInterface>(generated)`), whose methods each take the path as a `string`, optionally followed by a `CancellationToken`, and return `Task`, `ValueTask` or `void`
- **When** the app is built
- **Then** the generator emits a sealed implementation over `INavigator`, listed in `AiorosNavigators.All` with its interface, in which every method asks the navigator for that path with its token (`CancellationToken.None` without one), the methods of the interfaces it extends included; an interface registered twice gets one implementation and an interface never registered gets none.

*Precedent: Refit and our own server function clients implement an app-declared interface by generated code; ASP.NET Core's Request Delegate Generator finds the registration call at build time.*

## Program

### GENERATOR-36: Interfaces that cannot navigate are reported

- **Given** `AddNavigation<TInterface>` with a type that is not an interface, a generic interface, an interface the generated code cannot see, or an interface with a property or an event, a generic method, a method that does not take exactly a `string` path (optionally followed by a `CancellationToken`), or a method that returns anything but `Task`, `ValueTask` or `void` (a `Task<T>` included)
- **When** the app is built
- **Then**
  - Error `AIOROS016` is reported at the registration for each problem, naming the interface and the member and saying what a navigation method looks like, and no implementation is emitted for it.
  - There is no "back": `INavigator` has none, and on the web going back is the browser's own.

## Page head generator

### GENERATOR-37: Static titles and metadata are read from the XAML

- **Given** a page and a layout whose root elements set `a:Page.Title`, `a:Page.TitleTemplate`, `a:Page.AbsoluteTitle`, `a:Page.Description`, `a:Page.Canonical`, `a:Page.Image`, `a:Page.Type` or `a:Page.Indexable`
- **When** the app is built
- **Then** each definition carries them as its `Head`, read from the XAML text whether or not the file has a view model (the metadata typed `website` and indexable unless it says otherwise, and none when no metadata attribute is set); and a page or layout that sets none of them has no `Head`.

## Twice

### GENERATOR-39: View models with commands that cannot receive their data are warned

- **Given** a page's or layout's view model that takes its loader's data in its constructor, exposes a command (a public `ICommand` property, or a method marked with CommunityToolkit's `[RelayCommand]`) and state a command can change (a public property with a public setter, or a member marked with CommunityToolkit's `[ObservableProperty]`), and has no `Receive` for that data
- **When** the app is built
- **Then** warning `AIOROS017` is reported at the view model's declaration, so `[SuppressMessage]` can silence it there with a justification; it names the view model and the data and explains that after a command the view model is created again from fresh data and loses what the command set, and that a public `void Receive(TData)` keeps it; a view model with `Receive`, without commands, without settable state, or without loader data gets no warning.

## Recent loader

### GENERATOR-40: A view by its class brings its loader and view model

- **Given** a `UserControl` in `Views/Recent.axaml` with `x:Class` and `x:DataType`, a class implementing `ILoader<TData>` in `Views/Recent.Loader.cs` beside it, and a page that places `<v:Recent />` with `v` mapped to the class's namespace (`using:` or `clr-namespace:`)
- **When** the app is built
- **Then** no diagnostic is reported, the page's definition lists one view instance keyed by its host file and XAML position (`Pages/home/Page.axaml:0.1`, the element indexes from the root), in the page's segment (the number of layouts), whose view has the source `Views/Recent.axaml`, the class as its type, a loader that runs the class and a factory that passes the loader's data to the view model's constructor and the rest from DI, as a page's (GENERATOR-14).

### GENERATOR-41: Views are listed in document order with their segments and nesting

- **Given** a layout that places a view, a page that embeds another page with `<a:View Source="/Pages/inbox/Page.axaml" />` and a view whose own XAML places another view
- **When** the app is built
- **Then** the page's instances are listed in document order, outermost segment first, each with its segment, the nested one with its parent's index and a key that extends its parent's, the embedded page as a view of its own source while its route stays registered, and a view without a loader inside a list's item template is not an instance (its list supplies its model, GENERATOR-46).

### GENERATOR-42: A view without a loader gets its view model from dependency injection

- **Given** a view with no loader whose view model takes services
- **When** the app is built
- **Then** its view has no loader and its factory returns the view model registered in the provider, or builds it with its constructor's arguments from the provider, as a page's without a loader (GENERATOR-02).

### GENERATOR-43: Unresolvable views are build errors

- **Given** an `a:View` whose `Source` names no `.axaml` file of the project, or views that place each other in a cycle
- **When** the app is built
- **Then** the build reports error `AIOROS018` at the host file naming the missing source or the cycle, and no instance is listed for it.

### GENERATOR-44: Views carry their access and their data shapes in the pages contract

- **Given** a view's loader with `[Authorize(Roles = "admin")]`
- **When** the app is built
- **Then** the view carries that rule as its access and the page's own access is unchanged (a view the visitor may not see renders nothing, SSR-69); given a page with views whose loaders return data, its data contract changes when a view's data shape changes, and a page without views keeps the contract it had.

## Single view loader

### GENERATOR-46: A view in a lists item template repeats with its key and batch loader

- **Given** a page whose `ItemsControl` binds `ItemsSource="{Binding Cards}"` and whose `ItemTemplate`'s `DataTemplate` (`x:DataType` the item) places a view with a batch loader (`IBatchLoader<string, CardData>`) and `a:View.ItemKey="Id"` (a member path of the item)
- **When** the app is built
- **Then** the page lists one instance for it, keyed by the host file and the view element's XAML position as any instance, marked repeated with its list (the list's ordinal among the host's lists with an item template, a getter of the host model's `Cards`, a getter of the item's `Id` and a parser of a key from its text), and its view's loader loads many; without `a:View.ItemKey`, the instance has no key getter and the batch loader takes the items themselves; an item view without a loader is not an instance and the build reports nothing, since the parent supplies its model (the bound item).

*Precedent: React's `key` and LiveView's ids per stream item name each instance; Phoenix LiveComponent `update_many/1` and DataLoader batch the loads.*

### GENERATOR-47: A view with a loader for one instance in a list is a warning

- **Given** a view with a per-instance loader (`ILoader<T>`) placed in a list's item template
- **When** the app is built
- **Then** the build reports warning `AIOROS020` at the host file naming the view and the list, saying that its loader runs once per item (N+1 loads) and naming the two alternatives: let the parent supply the item (remove the loader; the bound item is the view's model) or load every item at once with `IBatchLoader<TKey, TData>`; the instance is still listed and its loader runs per item.

### GENERATOR-48: Views that cannot repeat are build errors

- **Given** a view with a batch loader placed outside any list's item template, a list whose `ItemsSource` is not a binding path of the host's view model, an `a:View.ItemKey` path the item type does not have, a batch loader whose key type is not the key's type (or, without `a:View.ItemKey`, the item type), or a view with a loader placed in a list nested inside another list's template
- **When** the app is built
- **Then** the build reports error `AIOROS021` at the host file with the reason, and no instance is listed for it.

### GENERATOR-49: Views chosen at run time are noted

- **Given** a view with a loader placed inside a template the generator cannot follow (a `DataTemplate` in `Resources` or `DataTemplates`, a `ContentTemplate`) and a project class implementing Avalonia's `IDataTemplate` (a `ViewLocator`)
- **When** the app is built
- **Then** the build reports info `AIOROS022` for each, naming the view and the template, or the locator, and saying that what they create is chosen at run time, so it is not composed with the page and its loader does not run with the page's; a view without a loader there (it takes the data it is given), and a view placed directly or in a list's item template, get no such note.

## Board

### GENERATOR-56: Converters are generated by their plans file reference

- **Given** an app that references Avalonia, with converters as public static members of its own types, converter classes with a public parameterless constructor and no settable property (one also a multi-value converter), and one class with a settable property
- **When** it is built
- **Then**
  - `AiorosConverters.Code` lists Avalonia's `BoolConverters`, `ObjectConverters` and `StringConverters` members and the app's by the reference the plans file names them with (`static:Namespace.Type:Member`, `type:Namespace.Type`), each with a typed `Convert` and `ConvertMany` that turn Avalonia's `UnsetValue`, `DoNothing` and binding notifications into no value, and a factory for a `MultiBinding`'s `StringFormat`; the class with a settable property is not listed; a plan's converter and multi-binding slots read from the plans file run through them without `Type.GetType` or `Activator`.
  - Without Avalonia nothing is emitted.

### GENERATOR-101: Static lists are generated by their plans file reference

- **Given** an app type with public static members that hold lists (an array of its own objects, a `List<string>`) beside a string and a number
- **When** the app is built
- **Then** the generated code lists each list by the reference a plans file names it with (`static:Namespace.Type:Member`) and the runtime's resolver reads it through a `static` slot from that code, without reflection; the string and the number are not listed, and a reference with no generated entry is read through the fallback.

*Precedent: GENERATOR-56, the converters named by `x:Static` and generated by the same reference.*

## Order

### GENERATOR-57: Types named in XAML are found by name without loading them

- **Given** XAML that names types in `x:DataType` and a `DataTemplate`'s `DataType`
- **When** the app is built
- **Then** `AiorosTypes.All` names each by its full name with a function that returns it, so a typed template's cases read from the plans file and a converter's target type are found without `Type.GetType`, and no type is loaded until it is asked for.

## Note

### GENERATOR-58: Command shapes are generated and awaited

- **Given** view models reached from the XAML with commands of several shapes (CommunityToolkit's `[RelayCommand]` on an async and a sync method, a property typed as a class with an awaitable `ExecuteAsync(object?)` returning `Task`, `ValueTask` or `ValueTask<T>`, one implementing it explicitly through an interface, one whose `Execute(TParam)` returns an `IObservable<T>`, and a plain `ICommand`)
- **When** the app is built
- **Then** `AiorosCommands.All` holds a runner per command type that awaits the command as `AwaitedCommands` does (the awaitable shape first, then the observable one, then plain `Execute`), matched by the exact type or, for an interface or base type, by being one; a post runs the command through it without expression trees.

*Precedent: the configuration binding generator replaces the reflection binder for the types it sees.*

### GENERATOR-59: Avalonia types are given as functions

- **Given** an app whose application class, view classes and a settable member's value type are Avalonia types
- **When** its generated entry point and registries are created
- **Then** none of those types is loaded: `AiorosApp.ApplicationType`, `ViewDefinition.Type` and `MemberAccessor.ValueType` are functions that return the type when it is asked for, and the accessors of an Avalonia type reached from the XAML (a `Point` a polyline draws) are listed apart in `AiorosAccessors.Deferred` by its full name and created the first time a plan reads a member of one.

## Badge

### GENERATOR-60: Validated view models keep their properties for the trimmer

- **Given** view models reached from the XAML, one of them validating through `INotifyDataErrorInfo` (CommunityToolkit's `ObservableValidator` reads its properties' validation attributes by reflection)
- **When** the app is built
- **Then** the generated accessors keep that type's public properties for the trimmer (`[DynamicDependency(PublicProperties)]` on `AiorosAccessors.All`), and only that type's, so its validation still finds its properties in a Native AOT publish.

*Precedent: the trimmer's `DynamicDependency`, which ASP.NET Core's generators emit for what they reach by reflection.*

## Old loader

### GENERATOR-61: Typed loader parameters are bound by name

- **Given** `Pages/shop/[id]/Page.Loader.cs` declaring a class (public or internal, sealed or static) that implements no loader interface and has one public method `LoadAsync` (or `Load`) taking `int id`, `string? q`, `int page = 1`, an enum `Sort sort = Sort.Top`, `Guid? owner`, a `ClaimsPrincipal`, the `PageRequest` and a `CancellationToken`, and returning `ValueTask<LoaderResult<TData>>`, `Task<TData>`, `TData` or `LoaderOutcome` (a loader without data)
- **When** the app is built and its definition runs for `/shop/7?q=red&page=3&sort=NEW&owner=…`
- **Then** the method gets 7, `red`, 3, `Sort.New`, the owner's `Guid`, the request's user, the request and the token, and without query values `null`, 1, `Sort.Top` and `null`: each parameter is bound by its name, from the route value of that name when the folder of the loader's file declares one and from the query otherwise, converted with the invariant culture (`string`, an enum by name ignoring case, any `IParsable<T>` such as numbers, `Guid` and dates, and their nullable forms); the loader's data type is the method's result, which its view model takes as with `ILoader<TData>`, and the generated code calls the method directly (an instance from the provider or its constructor, or the static method), with no reflection.

*Precedent: ASP.NET Core Minimal APIs bind a handler's parameters by name, route values first and the query otherwise, with the Request Delegate Generator emitting that code at build time.*

### GENERATOR-62: Unconvertible values answer not found or their default

- **Given** the loader of GENERATOR-61
- **When** its route value does not convert to the parameter's type (`/shop/abc` for `int id`, or a number out of range)
- **Then** the definition answers not found (`LoaderOutcome.NotFound`) without calling the method
- **When** a query value does not convert (`page=abc`, `page=`, `sort=nope`, `sort=3`, `owner=x`; a string takes any value, the empty one included)
- **Then** the parameter gets its default, or `null`, as when the value is absent.

*Precedent: SvelteKit's param matchers and Next.js `notFound` answer 404 for a path that names no page (Minimal APIs answer 400, an API's answer).*

### GENERATOR-63: Loader parameters that cannot be bound are build errors

- **Given** a loader method whose parameter is neither nullable nor has a default and names no route value of its file's folder (`int id` in `Pages/shop/Page.Loader.cs`), a parameter whose type cannot be converted from text (an object, a list, a `ref` parameter), a class with two public `Load` or `LoadAsync` methods, a generic one, or one returning no result (`Task`, `ValueTask` or `void`)
- **When** the app is built
- **Then** the build reports error `AIOROS025` at the parameter or the method, naming the loader and the reason (for the first, that query values are optional and the parameter needs `?` or a default), and no loader is emitted for it.

### GENERATOR-64: Loader results with a title are one call

- **Given** data, a title and metadata
- **When** a loader returns `LoaderResult.With(data, title, metadata)` (or `LoaderResult.With(data, title)`)
- **Then** the result carries the data, the title and the metadata, is neither a not found nor a redirect, and its type is `LoaderResult<TData>`, so a conditional with `LoaderResult.NotFound` needs no type argument.

## Form model

### GENERATOR-65: View models whose commands navigate are not warned

- **Given** view models with loader data, settable state and no `Receive` whose every command navigates (a `[RelayCommand]` method whose expression body or last statement calls, awaited or not, a method of `INavigator` or of an interface registered with `AddNavigation<T>`, both branches of an `if` doing so, other `return`s only as `if (HasErrors) return;` or as the last statement of a `catch` of `System.ComponentModel.DataAnnotations.ValidationException` that first calls an instance method of the view model, which implements `INotifyDataErrorInfo` (recording a server's refusal as field errors, as `ObservableValidator.ValidateAllProperties` does), and an `ICommand` property initialized with `new RelayCommand(Method)` or a lambda that navigates)
- **When** the app is built
- **Then** `AIOROS017` is not reported for them, since a command that navigates is not revalidated (SSR-55) and a command that leaves with field errors revalidates nothing (DESKTOP-24); a command that also returns early without errors, returns from a `catch` of `ValidationException` without calling the view model's validation, returns from a `catch` of another exception, navigates only in one branch, calls a navigation through a helper method, or an `ICommand` property whose command cannot be traced, still gets the warning.

## Secret

### GENERATOR-66: App types in a framework named namespace are walked

- **Given** view models declared in the app's own sources in a namespace that begins with a framework's name (`ReactiveUI.Samples.Suspension.ViewModels`, as ReactiveUI's own samples are written, or `Avalonia.Demo`), named in `x:DataType`
- **When** the app is built
- **Then** they are walked and get typed getters and setters like any other app type: whether a type is the app's is decided by where it is declared (the app's sources), not by its namespace, which only marks types from referenced assemblies as a framework's.

## Session

### GENERATOR-67: Referenced framework types the paths reach get typed accessors

- **Given** an app type whose property holds a type from a referenced assembly in a framework's namespace (ReactiveUI's `RoutingState`, outside `System`, the collections and `Avalonia`), XAML that reaches it through a property path (`{Binding Router.NavigateBack}`, also as `Path=` and on a `<Binding Path="...">` element) or names it in `x:DataType`, and another such type held by a property no path reads
- **When** the app is built
- **Then** `AiorosAccessors.All` holds a typed getter for each public instance property and field of the reached type (and a setter for each settable one), which returns what the member holds, while the type no path reads gets none and the reached type's own members are not followed further; the generated code names those members, so the trimmer keeps them in a Native AOT publish.

*Precedent: the configuration binding generator emits binding code for the referenced types its `Bind<T>` calls reach, from their metadata symbols.*

## Rx model

### GENERATOR-69: Methods bound as commands get method command accessors

- **Given** a view model with a method bound as a command in the XAML (`Command="{Binding SayHello}"`, Avalonia's binding to a method), its `CanSayHello(object)` guard, an asynchronous `ArchiveAsync(int)` and an overloaded `Twice` also bound
- **When** the app is generated
- **Then** its accessors include `SayHello` and `ArchiveAsync` (not the overloaded method, which Avalonia cannot bind either), each reading a `MethodCommand` that calls the method without reflection, awaited when it returns a task and with the parameter converted to its type (`"30"` to `30`), and whose `CanExecute` asks `CanSayHello`.

## View locator

### GENERATOR-80: A view with its own data context is no instance

- **Given** a page that places one view with a `DataContext` of its own (`<v:PlainItem DataContext="{Binding Cards[0]}" />`, as Ursa's layout places `PageMetadataView`) and another view without one
- **When** the app is built
- **Then** only the second is a view instance of the page (its key, its source); the first is no instance, gets no view model of its own and keeps the `DataContext` its binding gives it, as Avalonia does, and nothing is reported.

## Prefixed

### GENERATOR-111: Template selectors are rebuilt and run by generated code

- **Given** an app that references Avalonia, with a template selector class (`IDataTemplate`, a public parameterless constructor, a `Dictionary<string, IDataTemplate>` of templates, an enum-keyed one and an `IDataTemplate` property) and one with a constructor parameter
- **When** it is built
- **Then** `AiorosSelectors.All` lists the first by its full name with code that rebuilds it from a plans file's entries (member and key) with a stand-in template per entry and returns, for an item, the index of the entry its own `Build` picks, -1 when its `Match` refuses the item or `Build` uses none, without reflection and without starting Avalonia; the second is not listed, an entry naming an unknown member gives no chooser, and the runtime's resolver finds the chooser by the selector's name.

*Precedent: GENERATOR-56, converters rebuilt from their plans file reference by generated code.*

## Badge writer

### GENERATOR-112: Declared slot writers of the app and its libraries are registered by name

- **Given** an app that references a library assembly declaring two slot writers (`[assembly: SlotWriter<T>]` on types implementing `ISlotWriter`) and declares one itself
- **When** it is built
- **Then** the generated app hands the runtime one `SlotWriterEntry` per writer, named by the writer type's full name and creating it through the type's static `Create`, with no reflection.

### GENERATOR-113: Declared slot renderers are registered beside writers

- **Given** an app that references a library declaring a slot renderer (`[assembly: SlotRenderer<T>]` on a type implementing `ISlotRenderer`) beside a slot writer
- **When** it is built
- **Then** the generated app hands the runtime an entry for each, named by its type's full name: the renderer's created through its static `Create`, the writer's through its static `Create` adapted to a renderer that writes the value alone, with no reflection.

### GENERATOR-114: Renderer entries carry their decoders

- **Given** a library declaring a slot renderer that implements `ISlotRenderer.Decode` and one that does not, beside a slot writer
- **When** the app's generated registry is read
- **Then** each renderer's entry carries its decoder: the first decodes a composite post into its members' values, the second refuses every post (the interface's default), and the writer's entry has none.

*Precedent: C#'s static virtual interface members called through a constrained type parameter, which the generated entry uses instead of reflection.*

## Brand

### GENERATOR-115: Declared icon sets of the app and its libraries are registered

- **Given** a library that declares an icon set with `[assembly: IconSet<T>]` (`T` an `IIconSet` whose static `Icons` gives each kind's SVG markup) and an app that declares another
- **When** the app is compiled
- **Then** the generated `AiorosIcons.All` holds the icons of both, ordered by set name, and the app's `AiorosApp` carries them as `Icons` for the host to register at start, without reflection.

## Glyphs

### GENERATOR-116: Libraries without the runtime declare icon sets

- **Given** a control library compiled against the .NET framework and `Aioros.Abstractions` alone that declares `[assembly: IconSet<Lucide>]` with `Aioros.Icons.IIconSet`, and one compiled against the framework alone (as Aioros.Cosmo references only Avalonia) that declares its own internal `Aioros.Icons.IconSetAttribute<T>` and a public class with a static `Icons`
- **When** an app referencing both is generated
- **Then** `AiorosIcons.All` holds both libraries' icons, so the host registers them as it does any icon set: the attribute is matched by its full name, as the compiler matches polyfilled attributes, and `IIconSet` and `IconSetAttribute<T>` live in the abstractions (namespace `Aioros.Icons`), which reference nothing.

## Greeter

### GENERATOR-117: Framework types reached through an index get typed accessors

- **Given** an app type whose property holds a list of a type from a referenced framework (`IReadOnlyList<ReactiveUI.Frame>`) and XAML that reaches that type only through an integer index (`{Binding Router.Stack[1].Label}`, also as `Path=`)
- **When** the app is built
- **Then** the binding path is kept with its index and `AiorosAccessors.All` holds typed getters for the framework type's members.

## Picture loader

### GENERATOR-119: Pages whose models create Avalonia objects are marked

- **Given** pages whose page model, layout model, view model or loader creates an Avalonia object while it is constructed (a static or instance field or property initializer, a constructor or a static constructor, or a method or constructor of the app's own that one of them calls): a `Geometry` from `Geometry.Parse`, a `new SolidColorBrush`, an image (`Avalonia.Media.Imaging`), an asset (`Avalonia.Platform`) or a dispatcher or timer (`Avalonia.Threading`)
- **When** the page registry is generated
- **Then** their definitions carry `ModelsUseAvalonia = true`; a page whose models use only Avalonia's values (`Color.Parse`, `Thickness`, `KeyGesture.Parse`), create a converter whose lambda would build an image only when it converts, or use no Avalonia at all, does not.

## Item loader

### GENERATOR-123: Caches that could depend on the user are fixed

- **Given** the error `AIOROS024`
- **When** its code fix is applied
- **Then** on a server function's `[Cached(30, Shared = true)]` it drops `Shared` and keeps the lifetime (`[Cached(30)]`), so the results are kept per signed-in user; on a loader whose data depends on the user, it removes `[Cached]` (its attribute list too when it held nothing else); the rest of the file is kept as it was, and the error is gone.

## Aioros accessors

### GENERATOR-400: A project that only references an app generates none of its registries

- **Given** a project that references an assembly holding an Aioros app's generated `Aioros.Generated.AiorosPages` and has no page of its own (a test project that references the app, as ASP.NET Core's `WebApplicationFactory` tests do)
- **When** it is built
- **Then** the generators add none of the app's registries (`AiorosApplication`, `AiorosPages`, `AiorosFunctions`, `AiorosNavigators`, `AiorosAccessors`), so its code uses the app's own public `AiorosFunctions` and `AiorosNavigators` without `CS0436`
- **When** a project that references an app and has its own `Pages`, or a project that references no app
- **Then** they are generated as before.
