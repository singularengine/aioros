# Production

Plans compiled at build time and served without Avalonia (`src/Aioros.Runtime/Rendering/PlanSerializer.cs`, the runtime's slot resolver in `src/Aioros.Runtime/Slots` with the adapter as its fallback, `aioros build`).

25 criteria. Each has an identifier and exactly one test.

## Plan serializer

### PRODUCTION-01: Described plans round trip

- **Given** a plan whose slots are described (values with formats, conditions, repeats, ids, selects, scopes, choices)
- **When** it is serialized and read back through a resolver
- **Then** it renders exactly the same bytes for any model.

### PRODUCTION-02: Undescribed slots are reported

- **Given** a plan with a slot that has no description, at the top or nested in a list, a condition, a scope or a choice
- **When** it is serialized
- **Then** serialization fails and names the slot instead of writing a plan that would render differently, and it never throws, so the plans file keeps the other pages and reports this one (found porting AdvancedToDoList, where a nested slot aborted the whole precompile).

### PRODUCTION-04: Plans files keep pages and report the rest

- **Given** compiled pages for several routes and themes
- **When** they are written to a plans file and read back through a resolver
- **Then** each comes back with its route, theme, CSS and colors, and its plan renders the same bytes; a page whose plan cannot be serialized is left out and reported with the reason.

### PRODUCTION-07: Forms are applied through described fields

- **Given** a plan's fields
- **When** a form is applied without Avalonia
- **Then** a field without a description makes it decline (the engine posts instead), a malformed button value is a bad request, a button value that does not match the field's id and parameter is a conflict, and a matching one runs the command with its parameter in the button's scope.

### PRODUCTION-29: Plans that never read an outer scope keep none

- **Given** a page's plan read from the plans file
- **When** none of its parts names a `^` (no slot path reads an outer scope with `^depth`)
- **Then** its renders push no outer scope around repeats and nested bodies, so a getter that looks for one finds none, while a plan that names one, and a plan built in memory, keep every outer scope as before and render the same bytes.

## Production

### PRODUCTION-03: Compiled pages serialize

- **Given** a page that combines bound text, inputs, toggles, selects, ranges, dates, lists, tabs, pagers, icons, shapes and visibility
- **When** the engine compiles it to a reusable plan
- **Then** the plan serializes and, read back through the adapter's resolver, renders the same bytes for another model.

### PRODUCTION-06: Forms apply to plans like Avalonia

- **Given** a reusable page whose fields are bound two-way (text, check box, toggle, number, slider, date, time, combo box, list selection, pager) and a button with a bound command and a parameter
- **When** a form is applied to its plan without Avalonia
- **Then** the model ends in the same state and the page renders the same as after Avalonia's post of the same form.

## Page handler

### PRODUCTION-05: Precompiled pages skip the engine

- **Given** precompiled pages loaded at startup
- **When** a page is requested in a precompiled theme
- **Then** it is rendered from the loaded plan without calling the page engine, and warmup does not compile it again.

### PRODUCTION-08: Described forms post without the engine

- **Given** a page whose plan is cached and whose fields are all described
- **When** a form is posted
- **Then** the fields and the command are applied by the plan without calling the page engine, and the page is rendered from the plan or redirected with 303 when the model asks for it.

## Orders

### PRODUCTION-09: Apps are warned about blocking calls

- **Given** an app built with the Aioros package
- **When** its code blocks a thread (`Task.Result`, `Task.Wait`, `GetAwaiter.GetResult`, `Thread.Sleep`, wait handles)
- **Then** the build reports each call with RS0030 and says why and what to use instead; awaited code is not reported.

## Serial work

### PRODUCTION-10: Actions run one at a time in order

- **Given** actions sent to serial work, one after another and from several threads at once
- **When** they run
- **Then** no two run at the same time, those sent one after another run in that order, each caller resumes when its own action has run, and an exception reaches only the caller whose action threw.

## Precompile report

### PRODUCTION-11: Pages that do not compile fail the precompilation

- **Given** the outcomes of precompiling an app's pages
- **When** the report is written
- **Then** every page that compiled is listed as precompiled, a page whose model only redirects or is not found is listed as skipped, and every other page (a plan that is not reusable, route parameters without a sample URL, a load that throws, a plan that cannot be saved) is an `error AIOROS010` line with the route, the reason and what to do, led by the place in the XAML that kept the plan when it is known, as MSBuild and the IDEs read a diagnostic (`/app/Pages/lab/Page.axaml(12,6): error AIOROS010: /lab: …`, ENGINE-306); the report fails when there is any error, so the build stops instead of serving that page through Avalonia.

*Precedent: Next.js static export fails on a dynamic route without `generateStaticParams`.*

### PRODUCTION-14: Pages must render the same bytes with generated accessors and runtime delegates

- **Given** the precompiled pages of an app and the models each was loaded with
- **When** the plans file is checked
- **Then**
  - Each page and theme is rendered from the plans read with generated accessors and from the plans read with runtime delegates only, and the report says how many pages render the same bytes with both and how many members the generated accessors served out of all those read and written, how many of the writes among them, and names the members read or written at run time; a page whose bytes differ is an `error AIOROS010` line with the route, the theme and the offset of the first differing byte, and the report fails.
  - This is the check that both ways of reading a plan produce identical output.

### PRODUCTION-28: Every page left out states its reason

- **Given** precompilation outcomes whose reason is missing or says nothing (a page whose load threw an exception wrapped in a `TargetInvocationException`, one that threw an exception with an empty message, one recorded with a blank reason) and a page whose bytes differ between generated accessors and runtime delegates
- **When** the report is written
- **Then** every page left out of the count is a line that states a reason: the innermost exception's type and message (`InvalidOperationException: boom`), the exception's type alone when it has no message, `no reason was recorded` for a blank reason, and the header counts only the pages that compiled and render the same bytes both ways (`Precompiled 1 of 5 pages` after the check), so a report never says fewer pages compiled than it lists errors for.

### PRODUCTION-31: Pages whose plan a fresh model misses fail the precompilation

- **Given** a precompiled app
- **When** its plans are saved
- **Then** each page and theme is rendered from its saved plan for the page's models loaded anew, as a request loads them (never with the models its compile bound and the libraries wrote into), and a page whose plan misses for them (a `PlanMissException`) fails the precompilation with `error AIOROS010: <route> (<theme>): a fresh model misses its plan: <the miss>` and is no longer counted as precompiled, so a page the engine would render on every request does not pass silently (Aioros.Cosmo's `/components/command` answered 14 requests a second that way; iteration 18).

### PRODUCTION-35: Fields left to the engine are reported kept and preloaded

- **Given** a page whose verified posts left fields to the engine (a text box, a check menu item's press)
- **When** `aioros build` writes the plans file and reports
- **Then**
  - The report's line for the page names those fields and the first place the plan's page and the engine's differ (`the engine answers posts of f0 (text) … first difference: …`, not an error: the page is precompiled), the plans file keeps them with the page (`engine`), read back as written, and a plan cache preloaded from it leaves them to the engine (decision "Deterministic plan coverage", SSR-304).
  - A page verified within the budget (one sample a field, two of a radio group, at most `PlanParity.Budget` samples) names how many of its fields were left over (`12 fields not verified`), and a page whose models are shared says it was not verified, so no field goes unverified silently.

## Button controls

### PRODUCTION-12: Commands the plan cannot follow are left to the engine

- **Given** a button whose `Command` is bound through a path the plan cannot follow (a `$parent` lookup)
- **When** the page is compiled
- **Then** its field has no description, so a post to it is declined by the plan and handled by the engine instead of running nothing.

## Generated accessor

### PRODUCTION-13: Plans read and write through generated accessors and fall back to runtime delegates

- **Given** registered generated accessors for some members
- **When** a plan reads paths of several members, an outer scope (`^1.Member`), a converter's source and a command, and a form sets a two-way field
- **Then** the generated getters and setters are called for the members they cover (a getter that returns a marker shows it) and a member without one is read and written through the delegate compiled at run time; the coverage lists each member read with its owner type and whether a generated accessor served it; plans read with runtime delegates only never call a generated accessor.

### PRODUCTION-17: Precompilation counts the writes of two way fields without writing

- **Given** a plan whose page has two-way fields (a text box, a check box bound both ways) on a member with a generated setter and on one without, and a one-way check box
- **When** the plans file is checked at precompilation
- **Then** the setter each two-way field would write through is looked up for its scope's run-time type without writing it, so the coverage counts the writes no post has made (the member with a generated setter as served, the one without as written at run time) and the one-way field as no write, and the model keeps its values.

## Runtime slot

### PRODUCTION-15: The runtime resolver reads plans as the adapter does and hands the rest to its fallback

- **Given** the runtime's slot resolver with the app's generated accessors and no fallback
- **When** plans read from the plans file render member paths (with `^depth`), the self slot, flags, enabled states (a bound `IsEnabled` and a command's `CanExecute`), tests, pages, ranges, errors, the Avalonia-free formats (CSS number, invariant number, string format, identity, access key, offset, progress text, error text, except, date, time, icon, hex colors and points read through their `X` and `Y` accessors) and the selection predicates, and apply forms (text, check box, toggle, number, slider, date, time, combo box, list selection, pager and a command with its parameter)
- **Then** they read, format, select and bind as the adapter's resolver does with members read by reflection, and colors, points and angles as Avalonia converts them, and the formats written without a string (hex colors, CSS numbers, identity) write the same text as their formatters for every value they take, handing the others to the formatter; the icon format's markup is UTF-8 encoded once per kind and written as those bytes, the same as its formatter's text; a slot kind or value the runtime does not evaluate (a converter or a multi-binding without generated code, an `is` or `tag` variant, a chart, a brush, a thickness, a named color, a member without a generated accessor, a command type without a generated shape, a type without a generated entry) is handed to the fallback the resolver was given, and with no fallback it fails naming the slot instead of rendering something else.

*Precedent: ASP.NET Core's Request Delegate Generator keeps the reflection-based `RequestDelegateFactory` for what it cannot generate.*

## Production app

### PRODUCTION-16: A precompiled app serves pages and posts without loading Avalonia

- **Given** an app published with its plans file (Lumen)
- **When** it serves precompiled pages in production (one with route values, one in the dark theme) and a form post on a cached plan
- **Then** each answers as before (200, and the post's page with its saved state) and the process has loaded neither `Aioros.Avalonia`, `Avalonia.Base`, `Avalonia.Controls` nor SkiaSharp (`AppDomain.CurrentDomain.GetAssemblies`).

### PRODUCTION-32: Models that create Avalonia objects serve and post in production

- **Given** an app published with its plans file whose pages' models create Avalonia objects as they are constructed (one parses a `Geometry` in a static field, one creates a `SolidColorBrush` in its constructor) beside a page whose model uses no Avalonia
- **When** it serves them in production and then a post that the engine answers (one carrying a control's state, `_s0`)
- **Then** every page answers 200 and the post answers its page with 200: no `TypeInitializationException` from a model constructed on a request's thread before Avalonia started, and no engine that cannot start because a request's thread took Avalonia's UI dispatcher.

### PRODUCTION-33: A precompilation loads models as production does

- **Given** an app whose page's model creates an Avalonia object in a way the generator cannot see (through a service resolved from the app's services)
- **When** it is precompiled
- **Then** before Avalonia starts every page's models are loaded as production's warm-up loads them, in its order and on the threads requests use, and the precompilation fails with `error AIOROS032: <route>: its models created an Avalonia object on a request's thread before Avalonia started (...)`, or names the exception their construction threw there, and the pages after one whose request's thread took Avalonia's UI dispatcher are not checked (`the pages after it were not checked`), their failures being its consequence; a precompilation whose models create Avalonia objects only on Avalonia's thread (`ModelsUseAvalonia`) passes and reports `Production models: <n> of <n> pages load their models as production does`.

## Repeated view engine

### PRODUCTION-30: Answers are rendered without compiling a plan

- **Given** a page with repeated views
- **When** the Avalonia engine answers it for one request (`AnswerAsync`, what serves a page without a plan and a request its plan cannot write)
- **Then** it writes the same body as the render that compiles the plan, every rule of its style sheet among that render's, as literal text without slots, and the page is not reusable and carries no failure, without the plan recorder's learning work.

## Plans file

### PRODUCTION-34: Pages laid out alike share their text and stylesheet

- **Given** a plans file whose pages share text (two routes in both themes with the same layout literal and two of them with the same stylesheet)
- **When** it is read
- **Then**
  - Each distinct literal is one node shared by every page that has it and each distinct stylesheet one string, so a file of hundreds of pages laid out alike keeps each text once and encodes it to UTF-8 once, and every page renders the same bytes as before.
  - The file is read page by page from its UTF-8 bytes, never as one string or one document as large as the file: the Gallery's 130 MB file held about 1.5 GB at idle, most of it the read's leftovers.
