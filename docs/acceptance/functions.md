# Server functions

Service interfaces whose implementation always runs on the server: called in process on the web, and through a generated HTTP client from the installed app (`Aioros.Runtime/Functions`, the generator, and the dispatcher in `Aioros.Targets.Ssr/Functions`). An interface becomes server functions where it is registered, `builder.AddServerFunction<IOrders, Orders>("orders")`, which the generator finds at build time as ASP.NET Core's Request Delegate Generator finds `MapGet` calls and the configuration binder generator finds `Bind` calls; the contract references neither Aioros nor ASP.NET Core (principle 15), and who may call each function is `[Authorize]` on the server implementation, as on a controller action. Designed from the source of Leptos `#[server]`, TanStack Start `createServerFn` and SolidStart.

25 criteria. Each has an identifier and exactly one test.

## Composition

### FUNCTIONS-01: Clients call the server over HTTP

- **Given** an interface without attributes registered with `AddServerFunction<IOrders, Orders>("orders")`
- **When** the project is built
- **Then** its generated client implements it: each call is a `POST` to `/_sf/orders/{method name in camel case}` (or the name in `[ServerFunction]`) with the arguments as a JSON object named after the parameters, the headers `X-Aioros-Fn: 1` and `X-Aioros-Contract` with the interface's contract hash, and returns the JSON result; a method returning `Task` completes on `204`.

### FUNCTIONS-02: Groups run the implementation in process

- **Given** the same interface
- **When** the project is built
- **Then** the generated group invokes the server implementation in process with the arguments read from JSON and returns its result, and the contract hash is the same for the same signatures and data shapes and changes when a signature changes or a member of a record an argument or result holds is renamed or retyped (the data shapes are part of the hash since 2026-10-11, as for `/_data` in GENERATOR-28, so every contract changed once).

### FUNCTIONS-07: The server functions attribute still declares a group

- **Given** an interface marked `[ServerFunctions("orders")]` that no call registers, with `[Authorize]` on a method
- **When** the project is built
- **Then**
  - It gets the same client, group, routes and contract hash as the same interface registered with `AddServerFunction<IOrders, Orders>("orders")`, and the method's attribute is its access rule; given both, the registration's group wins.
  - (Deprecated fallback until step 1.9.)

### FUNCTIONS-08: Groups default to the interface name

- **Given** `AddServerFunction<IBoardFunctions, BoardFunctions>` without a group, written in any method or file of the project (an extension method that composes a feature, and the builder from the generated `AiorosApplication.CreateBuilder` included)
- **When** the project is built
- **Then** the group is the interface's name without its `I` prefix and its `Functions` suffix, in camel case (`board`; `IOrderBook` gives `orderBook`), the registry lists the group once even when two calls register it, and a call with the same name on another type is not a registration.

## Function

### FUNCTIONS-03: Calls are dispatched to the server implementation

- **Given** the dispatcher with an implementation in dependency injection
- **When** a function is called
- **Then** a well-formed call answers `200` with the JSON result written by the route's generated writer (a `null` result is the JSON `null`), or `204` for a function returning `Task`; a call without `X-Aioros-Fn` is `403`; a call with another contract hash is `426` with the server's hash in `X-Aioros-Contract`; an unknown function is `404`; an exception becomes problem details as FUNCTIONS-10.

### FUNCTIONS-04: Authorized functions check the user

- **Given** a function with access rules (each an optional policy and roles, as ASP.NET Core's `[Authorize]`)
- **When** it is called anonymously
- **Then** the answer is `401`; signed in without a rule's policy or roles, `403`; signed in satisfying every rule, the function runs; a function without rules runs for anyone.

### FUNCTIONS-06: Interfaces resolve to the implementation or the client

- **Given** an app with a server function interface and its implementation
- **When** it registers them without a remote server (the website, or the desktop app running the server in process)
- **Then** the interface resolves to the implementation, wrapped in the group's generated in-process caller when it has one (FUNCTIONS-12)
- **When** it registers them with a remote server address (the installed app)
- **Then** the interface resolves to the generated client whose calls go to that address.

### FUNCTIONS-11: Exceptions become problem details

- **Given** the dispatcher
- **When** the implementation throws
- **Then** the answer is `application/problem+json` (RFC 9457) written without reflection: a `System.ComponentModel.DataAnnotations.ValidationException` is `400` in ASP.NET Core's `ValidationProblemDetails` shape (`type` `https://tools.ietf.org/html/rfc9110#section-15.5.1`, `title` `One or more validation errors occurred.`, `status`, `detail` with the exception's message, and `errors` mapping each of its `ValidationResult.MemberNames` to `[message]`, omitted without member names); a `KeyNotFoundException` is `404` with its message as `detail`; an `UnauthorizedAccessException` is `401` for an anonymous caller and `403` for a signed-in one, with its message as `detail`; an `OperationCanceledException` is not answered; any other exception is `500` with the title `The server function failed.` and neither its message nor its type, and is logged.

*Precedent: ASP.NET Core's `ValidationProblemDetails` and `Results.ValidationProblem`, and .NET 10 Minimal APIs' validation (`AddValidation`), which answers a failed `[Required]` on a parameter with that `400`.*

### FUNCTIONS-13: In process and remote calls fail alike

- **Given** an implementation that throws a `ValidationException` with member names, a `KeyNotFoundException`, an `UnauthorizedAccessException` or an `ArgumentException`
- **When** a view model calls it in process through the in-process caller and from the installed app through the client and the dispatcher on a test host
- **Then** both calls throw the same exception type with the same message (over HTTP an anonymous refusal, `401`, is `SignInRequiredException`, which derives from `UnauthorizedAccessException`), and for validation the same member names.

### FUNCTIONS-18: Cross site calls are refused before the implementation runs

- **Given** the dispatcher and a well-formed call with `X-Aioros-Fn` and the right contract
- **When** it arrives with `Sec-Fetch-Site: cross-site` (or, without that header, an `Origin` of another host)
- **Then**
  - It is answered `403` and the implementation does not run; the same call with `Sec-Fetch-Site: same-origin`, or with neither header (the installed app's client), runs.
  - The page posts' rule (SSR-92..97) applies, trusted origins and bypassed routes (`/_sf/{group}/{name}`) included.

## Server function call

### FUNCTIONS-05: Failures become dot net exceptions

- **Given** the generated client
- **When** the server answers a failure
- **Then**
  - The call throws a .NET exception, so a view model catches it without referencing Aioros: `401` throws `SignInRequiredException` and `403` `UnauthorizedAccessException`, from which it derives (SIGNIN-07)
  - `404` throws `KeyNotFoundException`
  - `400` and `422` throw `System.ComponentModel.DataAnnotations.ValidationException` whose `ValidationResult` has the problem's `detail` (or its first field error, or its `title`) as the message and the keys of its `errors` member as the member names, in order
  - `426` throws `ClientOutdatedException` with the server's contract (the framework's concern, not the view model's); any other status throws `InvalidOperationException` with the problem's `title`; a body that is empty or not problem details gives each exception a default message.

## Program

### FUNCTIONS-09: Access rules come from the implementation

- **Given** a registered interface whose implementation has `[Authorize("admins", Roles = "owner")]` on one method, `[Authorize]` on its class with `[AllowAnonymous]` on another method, or `[Authorize]` on an explicit interface implementation
- **When** the project is built
- **Then** each route's access rules are the class's then the method's attributes, a method with `[AllowAnonymous]` has none, and a method of a class without attributes has none; the interface carries no attribute.

*Precedent: ASP.NET Core reads `[Authorize]` and `[AllowAnonymous]` on a controller and its actions.*

## Desktop container

### FUNCTIONS-10: App extensions need no using

- **Given** an app on the Web SDK with its implicit usings and no `using` of an Aioros namespace
- **When** its `Program.cs` calls `AiorosApplication.CreateBuilder(args)`, `builder.AddServerFunction<IOrders, Orders>`, `app.MapAioros` and `app.RunAioros`
- **Then** every one resolves, because the extension methods on `WebApplicationBuilder` and `WebApplication` are declared in `Microsoft.AspNetCore.Builder`, the namespace of those types, and the generator still finds the registration there.

*Precedent: ASP.NET Core declares its builder extensions in `Microsoft.AspNetCore.Builder` and `Microsoft.Extensions.DependencyInjection` so they need no extra `using`.*

## Setup

### FUNCTIONS-12: The in process caller fails as the client would

- **Given** a registered interface
- **When** the project is built
- **Then**
  - The generator also emits an in-process caller implementing the interface over the implementation: each call returns the implementation's result; a `ValidationException`, `KeyNotFoundException`, `UnauthorizedAccessException` or `OperationCanceledException` reaches the caller unchanged; any other exception, thrown synchronously or by the returned task, becomes an `InvalidOperationException` with the message `The server function failed.` and the original as its inner exception.
  - So a view model sees the same exception types and messages on the website as through the client.

### FUNCTIONS-14: Arguments and results use emitted code

- **Given** a registered interface whose arguments and results are strings, numbers, enums, records, lists, dictionaries, nullable values and polymorphic records (GENERATOR-33)
- **When** the project is built
- **Then** its client writes the arguments and reads the result, and its routes read the arguments and write the result, with emitted `Utf8JsonWriter` and `JsonElement` code shared with loader data (GENERATOR-27): no `JsonSerializer` and no reflection in the generated code; a missing argument is its type's default; a function returning `Task<T?>` that returns `null` gives `null` through the client.

*Precedent: System.Text.Json source generation and ASP.NET Core's Request Delegate Generator, which emit the (de)serialization of each endpoint at build time for Native AOT.*

### FUNCTIONS-15: Function data the generated code cannot send is reported

- **Given** a registered interface with a method whose argument or result is or holds, at any depth, data the generated code cannot write or read (the cases of GENERATOR-29)
- **When** the project is built
- **Then** the build reports error `AIOROS015` at the method, naming the interface, the method, the type, the member's path and the reason.

### FUNCTIONS-16: Implementations declare the keys their calls record

- **Given** a registered interface whose implementation declares `[Reads("issues", "issue:{id}")]` on one method, `[Invalidates("issues", "issue:{changes.Id}")]` on another and nothing on a third
- **When** the project is built
- **Then** the contract hash is the same as without the attributes (keys never reach the contract) and the in-process caller records, after the implementation completes: the read keys, with each `{name}` or `{name.Member}` replaced by the argument's value in the invariant culture, into the keys of the loader running it, and the invalidated keys, formatted the same way, into the keys of the command running it (`DataKeys.Current`); a call that throws records nothing; a call of the undeclared method records that the command called a function without key information.

## Keyed function

### FUNCTIONS-17: Calls answer and record the keys they invalidated

- **Given** the dispatcher
- **When** a function that invalidates keys succeeds
- **Then** the answer carries `X-Aioros-Revalidate` with those keys, each escaped as a URI component and separated by commas, as SolidStart's `X-Revalidate` names the keys an action changed; a function that invalidates nothing, or fails, carries no such header
- **When** the generated client, when a declared function's call succeeds
- **Then** the header's keys are recorded into the running command's keys, and a call of a function without key information records that it had none, whatever the header.

## Data cache

### FUNCTIONS-19: Loads share a cached read across requests

- **Given** a server implementation whose method declares `[Cached(60, Shared = true)]` and `[Reads("issue:{id}")]`, registered in process with the data cache
- **When** the loaders of three separate page loads call it with `7`, `7` and `8`
- **Then** the implementation runs once for `7` and once for `8` and the second load gets the first load's result instance; a call that fails is not kept, so the next load runs it again; and a call outside a load (a command's) never goes through the cache.

### FUNCTIONS-20: Invalidated keys evict their entries

- **Given** cached reads of `issue:{id}` for `7` and `8`
- **When** a call of a function declaring `[Invalidates("issue:{changes.Id}")]` for `7` succeeds, in process from a command or through the dispatcher, or a service calls `IDataKeys.Invalidate("issue:7")`
- **Then** the next load runs the read for `7` again while `8` stays cached; a call that fails evicts nothing; and a successful call outside a load of a function that declares no keys evicts every entry.

### FUNCTIONS-21: Cached reads expire with their lifetime

- **Given** a read declared `[Cached(60, Shared = true)]` and a fake `TimeProvider`
- **When** a load calls it at 0 s, 59 s and 60 s
- **Then** the implementation runs at 0 s and at 60 s only.

### FUNCTIONS-22: A full cache drops stale then soonest to expire

- **Given** a data cache limited to 4 entries
- **When** a fifth distinct call is kept
- **Then** the entries already expired or invalidated go first and, while the cache is still full, those that expire soonest, leaving at most three quarters of the limit before the new entry is added; the newest entries are served from the cache afterwards.

### FUNCTIONS-23: Data cached per user is never shared

- **Given** a read declared `[Cached(60)]` without `Shared`
- **When** loads call it for Ada, Ada again, Grace, and twice for an anonymous visitor
- **Then** Ada's second call is served from Ada's entry, Grace's call runs and gets her own result, and each anonymous call runs, since data cached per user is never shared across users and a visitor without an identity is never cached.

## Issue model

### FUNCTIONS-24: Saving a lumen issue evicts the cached list and detail

- **Given** Lumen's own issue functions (`samples/Lumen/Lumen/Data`), whose list and detail are cached and read `issues` and `issue:{id}`
- **When** the list and an issue are loaded twice and the issue is then saved through `SaveAsync`
- **Then** the second loads are served from the cache and the loads after the save run again and show the saved title.

## Data versions

### FUNCTIONS-25: Watchers receive invalidations in batches

- **Given** a watcher of the app's data versions (`DataVersions.WatchAsync`)
- **When** a background service invalidates `sales` through `IDataKeys` outside any command
- **Then** `issues` and `issue:7` are invalidated while the watcher has not read yet, then everything at once (`InvalidateAll`, a function without key information), then the watcher receives `sales` alone, then `issues` and `issue:7` together in one batch, then a batch marked as everything; once its token is cancelled it is removed at once (`Watchers` drops to 0) and later invalidations reach no one.

*Precedent: Phoenix.PubSub subscribes each process; Turbo 8 debounces the refreshes a burst of broadcasts causes.*
