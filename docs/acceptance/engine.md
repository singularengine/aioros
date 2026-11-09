# Page engine

The Avalonia page engine (`src/Adapters/Aioros.Avalonia/Hosting`): it builds a page with Avalonia headless, renders it once, and compiles it into a render plan that later requests fill without Avalonia. Its tests use real XAML pages.

236 criteria. Each has an identifier and exactly one test.

## Avalonia page engine

### ENGINE-01: Simple bindings compile to reusable plans

- **Given** a page whose bindings are simple property paths
- **When** it is rendered
- **Then** the result is reusable and its plan, rendered with another model, writes that model's values.

### ENGINE-02: Unplannable bindings fall back to the literal render

- **Given** a page with a binding the plan cannot model (a bound skew angle inside a `TransformGroup`)
- **When** it is rendered
- **Then** the result is not reusable, the failure names the property, and the literal render still shows the model's values.

### ENGINE-03: Bound visibility becomes a condition

- **Given** an element whose `IsVisible` is bound
- **When** the plan is rendered with the flag off
- **Then** the element is not written, and with the flag on it is; bound to the negated flag (`!Flag`), the element is written exactly when the flag is off.

### ENGINE-04: Bound items become a repeat

- **Given** an `ItemsControl` bound to a list
- **When** the plan is rendered with a model holding three items
- **Then** the item template is written three times with each item's values.

### ENGINE-05: Dark theme colors come from Avalonia

- **Given** the dark theme
- **When** a page is rendered
- **Then** the document colors come from Avalonia's dark theme and the style sheet declares `color-scheme:dark`.

### ENGINE-06: Pages render inside their layout

- **Given** a layout with an `Outlet` and a page
- **When** they are rendered
- **Then** the page is written inside the layout, and the layout's and the page's bindings each read their own model, also through the plan.

### ENGINE-07: Posted fields reach the model and run the command

- **Given** a form post with a text field and the clicked button
- **When** it is applied
- **Then** the text reaches the view model through the two-way binding and the button's command runs.

### ENGINE-08: Stale buttons conflict

- **Given** a form post whose button value no longer matches the page (the row changed)
- **When** it is applied
- **Then** the outcome is a conflict and no command runs.

### ENGINE-09: Reloaded XAML is used and broken XAML is rejected

- **Given** a page source reloaded with new XAML
- **When** the page is rendered again
- **Then** the new XAML is used
- **When** XAML that does not parse
- **Then** the reload returns the parser's message and the previous XAML stays.

### ENGINE-10: Literal values are encoded

- **Given** a value with HTML characters
- **When** the page is rendered literally
- **Then** the characters are encoded.

### ENGINE-11: XAML fragments render like pages

- **Given** a XAML fragment as text
- **When** it is rendered
- **Then** the result is the same HTML the engine writes for that XAML in a page (used by control tests and the designer).

### ENGINE-12: Pages are captured as png

- **Given** a page and a viewport size
- **When** it is captured
- **Then** the result is a PNG of that size rendered by Avalonia with Skia, with layout, for the compatibility bench.

### ENGINE-14: XAML fragments accept posts

- **Given** a XAML fragment and a form post
- **When** the post is applied
- **Then** the fields bind and the command runs as they do for a page.

### ENGINE-15: UI work runs on the UI thread

- **Given** work that changes view models bound to pages
- **When** it runs through the engine's `IUiThread`
- **Then** it runs on Avalonia's UI thread and its result reaches the caller off that thread.

### ENGINE-16: Application styles reload

- **Given** `App.axaml` reloaded with new application styles
- **When** a page is rendered again
- **Then**
  - The new styles apply and the old ones are gone
  - XAML that does not parse returns the parser's message and keeps the current styles.

### ENGINE-17: Tearing down keeps posted values

- **Given** a post that changes a value bound two ways (a pager's page)
- **When** the engine is done with the page
- **Then** the model keeps the posted value: tearing the page down never writes back into the model.

### ENGINE-18: Control themes have their own looks

- **Given** two controls of the same type and classes with different control themes
- **When** they are rendered
- **Then** each gets the look of its own theme.

### ENGINE-22: The page is marked inside its layout

- **Given** a page inside layouts
- **When** it is compiled
- **Then** the content of the innermost `Outlet` (the page) is written inside `<div data-aioros-page style="display:contents"><!--p-->` and `<!--/p--></div>`, the layouts outside it, in the planned and the literal render alike, and the plan stays reusable.

### ENGINE-23: Bindings the plan cannot reproduce are not frozen

- **Given** a binding the plan cannot reproduce (a `MultiBinding`, a converter the plans file cannot rebuild, such as one with settings of its own, or any binding no slot reads: on any property of a written control, such as a border thickness or a `Tag` no style selector reads, or of an object it holds, such as a transform in a `TransformGroup`)
- **When** the page is compiled
- **Then**
  - The plan is not reusable and its failure names the property and the reason, so nothing is frozen into a plan that would write another model's output wrong.
  - A converter the plans file can rebuild is run by the plan's path on any property (decision "Deterministic plan coverage", ENGINE-308).

### ENGINE-24: Bound classes render as Avalonia does

- **Given** `Classes.name="{Binding Path}"` (or its negation, `{Binding !Path}`) on a control and styles for that class (on the control and on its descendants, any property, also a property with a transition)
- **When** the page is compiled
- **Then** the plan is reusable and each render writes the control exactly as Avalonia renders it with the class when the value is true and without it otherwise (the end value of a transition, as on a first render): the same HTML and style rules as a page compiled with that value.

### ENGINE-25: Converters run as Avalonia runs them

- **Given** bindings with a converter (Avalonia's `BoolConverters`, `ObjectConverters` and `StringConverters`, or the app's own `IValueConverter` with a `ConverterParameter`, text or an enum value given with `x:Static`) on a text, a visibility, a bound class or a bound background or foreground
- **When** the page is compiled
- **Then** the plan is reusable and each render writes what Avalonia renders for that model, the converter running with the target type, the parameter and the culture as Avalonia calls it; the plan survives the plans file when its converters can be rebuilt (a static member of Avalonia's converter classes, of the converter's own type or of any class of the app that holds it, such as a static class of `FuncValueConverter`s as AdvancedToDoList's `BrushConverters`, or a type with a parameterless constructor and no settings), and is reported as not serializable otherwise.

### ENGINE-26: Items take the template of their type

- **Given** an `ItemsControl` whose items are of different types, or a `ContentControl` whose bound content is, and `DataTemplate`s by `x:DataType` (in the control's or an ancestor's `DataTemplates`)
- **When** the page is compiled
- **Then** the plan is reusable and writes each item or content with the template Avalonia picks for it (the first that matches, in Avalonia's order), for any mix and order of items, also after the plans file; a template is learned from the list's own item of its type when there is one, so members that need the item's state (a command it takes from its owner) render as Avalonia renders them; a content bound with a `StringFormat` is text, which Avalonia shows without a typed template (AdvancedToDoList's `Priority: {0}` beside the priority's icon).

### ENGINE-27: Multi bindings render as Avalonia does

- **Given** a `MultiBinding` (with a `StringFormat`, or with a multi-value converter such as `BoolConverters.And` or the app's own) on a text or a visibility
- **When** the page is compiled
- **Then** the plan is reusable and each render writes what Avalonia renders for that model, also after the plans file; a `MultiBinding` on a property the plan does not support stays a failure.

### ENGINE-28: Enabled states follow the model

- **Given** a control whose enabled state follows the model (a bound `IsEnabled`, or a command whose `CanExecute` enables it, with its parameter), with `:disabled` styles on it or its descendants
- **When** the page is compiled with the control enabled or disabled
- **Then** the plan is reusable and each render writes it enabled or disabled as Avalonia renders it for that model (the `disabled` attribute, the state's styles, its descendants disabled with it), also after the plans file.

### ENGINE-29: Bound data contexts scope their controls

- **Given** a control whose `DataContext` is bound to a path of its parent's data context
- **When** the page is compiled
- **Then** the plan is reusable and each render writes the control and its descendants from the value at that path, as Avalonia renders them for that model, also after the plans file.

### ENGINE-30: Ancestor bindings read their scopes

- **Given** bindings in an item template to an ancestor's data context (`$parent[Type]`, `$parent[Type;level]`, `#name` or `RelativeSource AncestorType`, then `DataContext` and a path, also a compiled binding that casts it, `((m:Model)DataContext)`, with `m` mapped by `using:` and the page loaded at run time as in development), such as a command of the page's view model with the item as its parameter
- **When** the page is compiled
- **Then** the plan is reusable, each render writes what Avalonia renders for that model, also after the plans file, and a posted button runs the ancestor's command with its item's parameter.

### ENGINE-31: Bound values with few possibilities render each

- **Given** a bound property with a few possible values that only changes how Avalonia lays out or styles a control (an enum such as an alignment, or `Grid.Column`, `Grid.Row` and their spans in a grid)
- **When** the page is compiled
- **Then** the plan is reusable and each render writes the control as Avalonia renders it for the model's value: an enum converted from text as Avalonia converts it (a value it cannot convert leaves the default), and a grid cell clamped to the grid's columns or rows, also after the plans file.

### ENGINE-32: Bound style values are inline styles

- **Given** a bound `Opacity`, `Margin` or (on a `Border`) `BoxShadow`, a single bound rotate, scale or translate transform, or a bound `FontSize` on a `TextBlock`, on an element written as a container, text or icon
- **When** the page is compiled
- **Then** the plan is reusable and each render writes the model's value, converted as Avalonia converts it, in the element's inline style, the same HTML as a page compiled with that value, also after the plans file; on a control whose writer does not open its element with the shared root writer (a `ListBox`) the binding is reported and the literal render keeps the value; controls written by their own writer follow ENGINE-158.

### ENGINE-33: Bound flags convert as Avalonia does

- **Given** a bound visibility or a bound class whose value is not a `bool` (a number, text, null, or a path the model's type does not have), plain or negated with `!`
- **When** the page is compiled
- **Then** the plan is reusable and each render shows or hides the element and sets the class as Avalonia does for that model: `!` converts with `Convert.ToBoolean` (null and zero are false), a plain path converts as Avalonia's default converter does, and a value neither converts leaves the property's default, also after the plans file.

### ENGINE-34: Element bindings read the bound path

- **Given** a binding to another element's property (`#name.Property`, or `$parent[Type].Property`, negated or not: `!#name.Property`, as Ursa's demo hides its menu's title with `!#menu.IsHorizontalCollapsed`) that is itself bound to a path of the model
- **When** the page is compiled
- **Then** the plan is reusable and each render writes the model's value at that path, as Avalonia renders it, also after the plans file
- **When** a binding to an element itself (`Label.Target="{Binding #name}"`), which reads no model data
- **Then** the plan is reusable.

### ENGINE-35: Fields with several elements share their ID

- **Given** fields with several elements (a three-state check box's radios, a radio group's members) after fields written more than once in the plan (buttons whose enabled state follows the model)
- **When** the page is compiled
- **Then** the plan is reusable and each render names every element of a field with the id that field takes in that render, as Avalonia's render does, also after the plans file.

### ENGINE-36: Bound padding and corner radius are inline styles

- **Given** a bound `Padding` or `CornerRadius` on a `Border`
- **When** the page is compiled
- **Then** the plan is reusable and each render writes the model's value in the element's inline style, `padding` and `border-radius` in px as a page with that value written in XAML has them in its class, also after the plans file; a value Avalonia does not convert to a `Thickness` or a `CornerRadius` (a number or text, whose binding fails, the same for a bound `Margin`) writes the property's default, zero; given a bound `Padding` on a control written without the shared root writer (a `ListBox`; for the controls written with it see ENGINE-159), the plan is not reusable, its failure names `ListBox.Padding`, and the literal render keeps the value.

### ENGINE-37: Bound skew and matrix transforms are inline styles

- **Given** a bound `SkewTransform` angle (`AngleX`, `AngleY`), a bound `MatrixTransform.Matrix`, or a `RenderTransform` bound to a transform or to text, on an element written as a container, text or icon
- **When** the page is compiled
- **Then**
  - The plan is reusable and each render writes the matrix Avalonia computes for the model's value in the element's inline style, the same `transform` a page with that transform written in XAML has in its class (text parsed as Avalonia's `TransformConverter` parses it), and the element keeps its `RenderTransformOrigin` as `transform-origin` whatever the compiled model's value, also after the plans file.
  - A value Avalonia does not convert (a matrix as text, text that is not a transform, null) writes the property's default (the identity matrix, or no transform); given a bound transform inside a `TransformGroup`, or a bound `RenderTransform` on a control written without the shared root writer (a `ListBox`), the plan is not reusable, its failure names the property, and the literal render keeps the value.

## Items controls

### ENGINE-19: Empty lists learn their template from the data type

- **Given** an `ItemsControl` and a `ListBox` whose bound lists are empty when the page is compiled and whose item template declares its `x:DataType`
- **When** the page is compiled
- **Then**
  - The plan learns the item template from a stand-in item (the slots come from the bindings' paths, not from values) and is reusable; rendered later with items, it writes the same HTML and the same style rules as a page compiled with those items, and the page stays as Avalonia renders it (empty).
  - Without a declared data type (or without an item template, as a `ListBox` of texts in Ursa's demo), the stand-in is of the type of the bound list's items (its `IEnumerable<T>`); a list whose items have no known type (an `ArrayList`) still fails as an empty list.

### ENGINE-20: String formats are planned

- **Given** a text bound with a `StringFormat` (`{Binding Count, StringFormat='{}{0} sold'}`)
- **When** the page is compiled
- **Then** the plan is reusable and writes each model's value formatted the way Avalonia formats it (`string.Format` with the current culture), also after the plan is saved to and read from the plans file.

### ENGINE-21: Templates bound to the item are planned

- **Given** an item template that binds to the item itself (`{Binding}`, as in a list of strings)
- **When** the page is compiled
- **Then** the plan is reusable and writes each item.

### ENGINE-333: Empty lists learn their template from an abstract data type

- **Given** an `ItemsControl` whose bound list is empty when the page is compiled and whose item template declares an interface as its `x:DataType`, one declaring an abstract class with a concrete class in the same assembly, and one declaring an abstract class with none
- **When** the page is compiled
- **Then** the first two plans learn the template from a stand-in and are reusable, and rendered later with items they write the same HTML as pages compiled with those items, as ENGINE-19 does for a concrete type (the stand-in is an instance of a concrete class of the declared type, the first by name among the loaded assemblies that can name it; only the bindings' paths are read from it); the third is not reusable and names the empty list, as no instance can be made.

*Precedent: dependency-injection assembly scanning (Scrutor), which finds an interface's implementations among the loaded assemblies.*

## Selected tag

### ENGINE-39: Selectors on a bound tag render each value

- **Given** a control whose `Tag` is bound to a path of the model and read by style selectors (`Border[Tag=on]`, `Border[Tag=off] TextBlock`, a nested `^[Tag=1]`, in any `Styles` or `ControlTheme` that may apply to the control or its descendants)
- **When** the page is compiled
- **Then**
  - The plan is reusable and writes the control once for each distinct value the selectors' texts take converted to the path's type as Avalonia's `PropertyEqualsSelector` converts them (the text itself for a string, compared ordinally; the type's converter with the invariant culture otherwise, so `[Tag=1]` and `[Tag=01]` both match the number 1), and once matching none; each render writes the control as Avalonia renders it for the model's value, a value no selector matches (`ON` against `[Tag=on]`, a number no text converts to) included, also after the plans file.
  - A bound `Tag` stays not reusable, the failure naming the property, when its path's type is not sealed (an `object`, whose values Avalonia converts by their own type), when it is bound with a converter or a `StringFormat`, and when no selector reads it (ENGINE-23); a text the type's converter rejects gives no variant, as Avalonia's own render throws on it for any value of that type.

*Precedent: Avalonia's `PropertyEqualsSelector.Compare`; the variants follow ENGINE-24 and ENGINE-31.*

## Awaited commands

### ENGINE-40: Toolkit async commands are awaited

- **Given** a command in CommunityToolkit's shape (`IAsyncRelayCommand`: a public `ExecuteAsync(object?)` that returns a `Task`)
- **When** it is run for a post
- **Then** `ExecuteAsync` is called once with the parameter and the run ends only when that task ends.

*Precedent: CommunityToolkit's `AsyncRelayCommand.ExecuteAsync` and `ExecutionTask`.*

### ENGINE-41: Awaitable execute async is awaited

- **Given** a command in Prism's shape (`AsyncDelegateCommand`: `ExecuteAsync(object?)` declared on an interface it implements explicitly, `IAsyncCommand`, returning a `Task`; or a public one returning a `ValueTask`)
- **When** it is run for a post
- **Then** that `ExecuteAsync` is called once with the parameter instead of `ICommand.Execute` (an `async void` in Prism), and the run ends only when what it returned ends.

### ENGINE-42: Observable execute is awaited until it completes

- **Given** a command in ReactiveUI's shape (`ReactiveCommandBase<TParam, TResult>`: a public `Execute(TParam)` that returns a cold `IObservable<TResult>`)
- **When** it is run for a post
- **Then** the observable is subscribed to once, with the parameter cast to `TParam` (`default` for null, as ReactiveUI's `ICommand.Execute` does), and the run ends only when the observable completes, after its values.

### ENGINE-43: Plain commands run synchronously

- **Given** a plain `ICommand` (none of the shapes above, such as CommunityToolkit's `RelayCommand`)
- **When** it is run for a post
- **Then** `Execute` is called once with the parameter and the run has ended when it returns
- **When** any command whose `CanExecute` is false for the parameter
- **Then** nothing is called.

### ENGINE-44: Failing commands fail the run

- **Given** a command that throws from `Execute` or `ExecuteAsync`, returns a faulted task, or whose observable signals an error
- **When** it is run for a post
- **Then** the run fails with that exception, so the post fails as a page that throws does (the website answers 500 with its error page) instead of rendering as if the command had succeeded.

### ENGINE-45: Posts answer after the command ends

- **Given** a page whose button's command is asynchronous (any shape of ENGINE-40 to ENGINE-42)
- **When** a form that presses it is applied, by the Avalonia engine or by the plan without Avalonia
- **Then**
  - The post answers only after the command has ended, so the page it renders shows the state the command left.
  - The command's shape is found once per command type, the first time one runs, and kept, as compiled getters are (`PropertyPaths`); no toolkit is referenced.

*Precedent: ASP.NET Core's `ObjectMethodExecutor` (`AwaitableInfo`, `CoercedAwaitableInfo`) awaits a return type found awaitable by its shape, once per method.*

## Live compile

### ENGINE-46: Live changes wait for the compile

- **Given** a live model fed through `IUiThread.InvokeAsync` (a feed that changes it on the UI thread)
- **When** a page bound to it is compiled and a change is sent while the compile runs (the compiler runs the dispatcher's queue, to realize a list)
- **Then** the change waits until the compile is done: Avalonia's render and the plan's are taken from the same state, the plan is reusable instead of failing the plan-versus-render check, and the change is applied right after, completing the feed's `InvokeAsync`.

*Precedent: WPF's `Dispatcher.DisableProcessing` holds the dispatcher's queue while a layout pass runs, and React's `useSyncExternalStore` reads one snapshot of an external store for a whole render so that no tearing occurs.*

## View engine

### ENGINE-47: A view reads its own model inside the host

- **Given** a page that places a `UserControl` with `x:Class` and its own view model in the slot after the page's
- **When** it is compiled with the page's and the view's models
- **Then** the result is reusable, the view's region reads the view's model and the rest of the page the page's, and rendered with other models both change.

### ENGINE-48: A page embedded by its source renders from its slot

- **Given** a page that embeds another page with `<a:View Source="…" />`
- **When** it is compiled with a model for the embedded page in its slot
- **Then** the embedded page's markup is written inside the view's region from that model and the plan reproduces it with another model.

### ENGINE-49: Bindings across a view boundary are reported

- **Given** a view whose control binds across its boundary into the host (`$parent[UserControl].DataContext.Title`, reaching the page's model)
- **When** the page is compiled
- **Then** the result is not reusable and the failure names the binding and says that it reaches across the view's boundary into its host.

### ENGINE-50: Failed views show their error region

- **Given** a view whose slot holds the failed marker
- **When** the page is compiled
- **Then** Avalonia's render shows the app's `Pages/ViewError.axaml` (or, without one, the built-in message) in the view's error region and the result is not reusable
- **When** a plan recorded with the view loaded, when it is rendered with the failed marker
- **Then** it writes the same error region, and with the hidden marker nothing of the view.

## View form

### ENGINE-51: Fields are named per view instance

- **Given** a page with a check box, a text box and a button around two instances of the same view (each with its own check box, text box and button)
- **When** it is compiled with a model per instance
- **Then** Avalonia's render and the plan agree (the result is reusable), the page's fields are named `f0`, `f1`, … in document order and each instance's `f{slot}.0`, `f{slot}.1`, …, and each instance's button posts `_btn` as `{slot}.{n}|parameter`, its buttons numbered from `0` apart from its fields (ENGINE-210).

### ENGINE-52: A post binds only its views fields

- **Given** that page
- **When** the form is posted with every field's value and the second instance's button, through the plan's forms and through the engine alike
- **Then** only the second instance's fields are bound and only its view model's command runs; the page's and the first instance's check boxes, absent from the post as unchecked boxes are, keep their values; a page button binds only the page's fields and runs the page's command; a button id that is not on the page (`{slot}.9|…`) is a conflict and nothing runs.

### ENGINE-207: A button with another parameter is refused before binding

Given the page of ENGINE-52, when a post carries every field and names the second instance's button with a parameter it was not written with (`2.0|forged`), then, through the plan and through the engine alike, the post is refused as a conflict before any field is bound: the second instance's name keeps its value and no command runs.

## Repeated view engine

### ENGINE-53: Each item of a list takes its own view model

- **Given** a page whose list's item template places a view with a batch loader, compiled with the item models of its slot keyed by the items' keys
- **When** it is rendered
- **Then** each item's view control takes its own model from the slot by its key (Avalonia's render and the plan agree, so the result is reusable), each item is written in its `{key}[{item}]` region, and the plan rendered with other item models writes theirs
- **When** an item view without a loader
- **Then** it renders inside the list with the bound item as its model and no region of its own (the parent supplies it).

### ENGINE-54: A post binds only its items fields

- **Given** that page with a text box and a button in each item's view
- **When** the form is posted with every item's field and the second item's button, through the plan's forms and through the engine alike
- **Then** only the second item's field is bound and only its view model's command runs; a button naming an item that is not in the list is a conflict and nothing runs.

## Capture

### ENGINE-55: States are forced on input elements before the frame

- **Given** a page with a button and a capture state
- **When** it is captured
- **Then** before the frame is taken every input element carries that state's pseudo-classes with its transitions off (`pointerover`: `:pointerover`; `pressed`: `:pointerover` and `:pressed`; `focus`: `:focus` and `:focus-visible`), so the button's pixels differ from the `normal` capture and between `pointerover` and `pressed`; an unknown state is rejected with an `ArgumentException`.

*Precedent: Chromium's DevTools `CSS.forcePseudoState`, which the compatibility bench uses on the web side.*

### ENGINE-56: Visible texts are listed with their bounds

- **Given** a page with a `TextBlock` "Hello" at margin 10,20, a hidden `TextBlock` and a `TextBox` holding "World"
- **When** it is captured
- **Then** the capture lists each visible text with the bounds of its glyph lines in window pixels ("Hello" from x = 10 and y = 20 with a positive width and height, "World" inside the text box) and not the hidden one, for the compatibility bench's layout comparison against the browser's `getClientRects`.

### ENGINE-57: Disabled elements keep their look in every state

- **Given** a page with a disabled `TextBox`
- **When** it is captured in the `pointerover`, `pressed` and `focus` states
- **Then** it keeps the look of the `normal` capture (its disabled background), since a disabled control never gets the pointer or the focus in Avalonia (and a browser never focuses a disabled field): the capture forces the states only on enabled input elements.

### ENGINE-65: An object of another thread fails its capture alone

- **Given** a page whose model holds a brush created on another thread
- **When** it is captured
- **Then**
  - The capture fails with an `InvalidOperationException` naming the control, the property and Avalonia's rule (an Avalonia object belongs to the thread that created it, `VerifyAccess`), before Avalonia's renderer reads it; and the next capture, of another page, renders its frame.
  - Without the check the renderer's commit throws half-way and leaves the compositor waiting on a batch it never sends, so every later capture renders no frame.

### ENGINE-263: Animations are captured as the browser shows them

- **Given** a page with a red box whose style runs a 10 s keyframe animation of its opacity from 1 to 0 (kept at its end) and a red box whose style runs an endless 0.7 s animation of its opacity from 0 to 1
- **When** it is captured for the compatibility bench
- **Then** the first box is captured at its end (opacity 0, the white page) and the second at its start (opacity 0, white), however long the capture takes, as the bench's browser side finishes finite animations and parks endless ones at their start: the capture runs the page's animations on a clock of its own, pulsed to its start and then far past every finite animation's end at a time every common duration divides; a transition a forced state starts lands at its end the same way.

## Binding source

### ENGINE-60: Static sources are written as rendered

- **Given** bindings with an explicit `Source` that is not the page's data (`{Binding Source={x:Static ...}, Path=...}`, `{Binding Source={StaticResource ...}, Path=...}` on a resource, and a markup extension that returns a `Binding` whose `Source` is a shared object raising `PropertyChanged`, as Kartova's `{l:T Key}` does), on a text, a negated visibility, a bound class, a property with a converter the plan does not convert (a `Tag`) and a `MultiBinding` whose bindings all have such a source
- **When** the page is compiled
- **Then**
  - The plan is reusable and each render, also after the plans file, writes what Avalonia rendered at compile whatever the model.
  - Avalonia resolves `Source` once, when the binding is created, and reads no data context through it, so the value is app state (a language, a static setting), which the plan takes at compile as it takes a `DynamicResource` (one plan per theme); a change the source raises later is applied by Avalonia to open windows, as a resource change is, and by the plan when the page is compiled again.

*Precedent: Avalonia's `ReflectionBinding.CreateInstance` and `BindingExpression`, which keep `Source` as the expression's source instead of a `DataContextNode`.*

### ENGINE-61: Source that is a data context reads its scope

- **Given** a binding whose explicit `Source` is the data context of its control or of an ancestor (the page's model, set as `Source` in code)
- **When** the page is compiled
- **Then** the plan is reusable and each render writes the value at that path of the model it is given, as Avalonia renders it, also after the plans file.

### ENGINE-62: Sources the plan cannot follow are reported

- **Given** a binding whose explicit `Source` the plan cannot follow (an element, such as the page's root returned by a markup extension), or a `MultiBinding` that mixes a binding with a `Source` of ENGINE-57 and a binding to the data context
- **When** the page is compiled
- **Then** the plan is not reusable and its failure names the property and says that it is bound through `Source` to what the plan cannot follow, instead of reading the path from the page's data.

## Method command

### ENGINE-63: Method bindings are planned as commands

- **Given** buttons whose `Command` is bound to a view model method, which Avalonia runs as a command (`Command="{Binding SayHello}"`, and an asynchronous method taking an `int` with `CommandParameter="30"`)
- **When** the page is compiled
- **Then** the plan is reusable, the button is disabled when the method's `Can<Method>(object)` returns false and enabled when it returns true, as Avalonia's `MethodToCommandConverter` decides
- **When** its button is posted
- **Then** the plan runs the method, awaited, with the parameter converted to the method's parameter type, as Avalonia's render does, and runs nothing when `Can<Method>` refuses.

### ENGINE-64: Command actions are planned by their shape

- **Given** a button whose `Command` is a command action set by a markup extension (shaped as Stylet's `{s:Action Wave}`: a command with a public `MethodName` and a `Target` handed down from an inherited attached property bound to the data context)
- **When** the page is compiled
- **Then** the plan is reusable, the button follows the target's `Can<Method>` guard property, and a post runs the method on the target through the plan as Avalonia's render does
- **When** an action whose target has no such method
- **Then** the plan is not reusable and its reason names the action (`Button.Command is the action 'Missing' (ShapedAction)`).

### ENGINE-121: Template buttons run their controls methods through Avalonia

- **Given** a control template whose button runs a method of the templated control through an ancestor binding (`Command="{Binding $parent[m:Clearable].Clear}"`, as Ursa's pickers and `NumericUpDown` clear themselves with `$parent[IClearControl].Clear`), on a control the page places
- **When** the page is compiled
- **Then** the plan is reusable, each render writes the button as Avalonia renders it, enabled, whatever the model, and the button's field is not described, so its post goes through the Avalonia engine, which runs the method on the control (the plan cannot run a control's code), and a binding to another part of the same template by its name (`{Binding !#ClearButton.IsVisible}` on Ursa's picker icons, where that button is hidden in the template and runs such a command) is written as rendered, since that part reads no model data
- **When** the same binding to a method whose type has a `Can` guard Avalonia's method commands read (`CanReset`)
- **Then** the plan is not reusable and the reason names the binding.

*Precedent: Avalonia's `MethodToCommandConverter`, which enables a method command unless a `Can<Method>` guard says otherwise.*

## Prompt post

### ENGINE-75: Posts answer once a command waits on a prompt

- **Given** a page whose button runs an asynchronous command that opens a modal prompt in a host panel of the page (a control added to a panel that is not an items panel, as Ursa's `OverlayDialogHost` adds a dialog and its mask) and then awaits the user's answer
- **When** the button is posted
- **Then** the post answers `Rendered` once Avalonia has run all its queued work, leaving the command waiting as Avalonia's own window would, instead of never answering
- **When** a command that only adds an item to a bound list (a container added to an items panel) before it awaits
- **Then** nothing is taken as opened and the post awaits the command to its end before answering.

### ENGINE-143: A prompt is written as a dialog over its host

- **Given** a page whose button runs a command that opens a prompt in a host panel of the page and awaits it (ENGINE-75)
- **When** the button is posted
- **Then** the outcome reports that the answer shows what the post left for the user, and its HTML writes the prompt's controls after the page, in a `<dialog open class="prompt">` placed on the host's box through CSS anchor positioning (the host's class carries `anchor-name`, the dialog `position-anchor`) and holding a hidden `_prompt` field that names the button that opened it; the page's fields keep the names they had before the prompt opened.

### ENGINE-144: A prompts button resumes the command that waits

- **Given** that answer
- **When** a button of the prompt is posted with the `_prompt` field, the page's fields and a field of the prompt
- **Then** the engine replays the opening on the fresh page (binds the page's fields and presses the opening button, as Web Forms rebuilds its page before it raises a postback's event), binds the prompt's field and presses its button: the command resumes with the prompt's answer and runs to its end, and the HTML holds no prompt.

### ENGINE-145: A prompt left open records its steps and replays them

- **Given** a prompt whose button changes it and leaves it open
- **When** that button is posted
- **Then** the answer shows the prompt again with its change, and its `_prompt` field records both steps, the second with the prompt's field values
- **When** the prompt's closing button is posted next
- **Then** both steps are replayed before it and the command ends once, with the change of the second step and the answer typed last.

### ENGINE-146: A replay that diverges is a conflict

- **Given** a post whose `_prompt` field names a step that does not open a prompt again (the page changed, or a value written by hand)
- **When** it is posted
- **Then** the answer is `Conflict` and no step after the one that diverged runs
- **When** a malformed `_prompt`
- **Then** the answer is `BadRequest`.

### ENGINE-147: A notification is written as a notice at its corner

- **Given** a command that shows a notification through Avalonia's `WindowNotificationManager` (a content control added to a panel outside the page, in the window's adorner layer)
- **When** its button is posted
- **Then** the outcome reports that the answer shows what the post left, and its HTML writes the card after the page in a `<div popover="manual" class="notice">` placed at the manager's corner (`top:0` and `right:0` for `TopRight`), while a command that shows nothing reports nothing; a notice shown by a step that a later post replays is not written again.

### ENGINE-148: A posted button raises its click before its command

- **Given** a button that handles its `Click` event in code (as an overlay dialog's OK button closes its dialog from its template)
- **When** its post is applied
- **Then** its `Click` event is raised and then its command runs, as Avalonia's `Button.OnClick` does; a handler that marks the event handled keeps the command from running.

### ENGINE-272: A prompts steps replay with the browsers line breaks

- **Given** a prompt whose `_prompt` field records two steps (ENGINE-145)
- **When** its closing button is posted with the steps' line break sent as CR LF, as browsers send every line break of a form's values (HTML's form submission normalizes them)
- **Then** the engine replays the same two steps and the command ends as with LF (the change of the second step kept, the answer typed last).

*Precedent: the HTML standard's newline normalization when a form's entry list is encoded.*

### ENGINE-273: Only the prompts own controls resume it without the script

- **Given** a command waiting on a prompt (ENGINE-143)
- **When** the answer is written
- **Then** beside its `_prompt` field the page carries a hidden `_answers` field naming the tokens of the prompt's buttons and fields; and when the answer's form is posted as a browser without scripting posts it, the prompt's button resumes the command through its steps, while a button of the page outside the prompt runs its own command alone, the prompt's steps not replayed.

### ENGINE-301: A prompt replays at most sixty four steps

- **Given** a prompt's post whose `_prompt` holds more than 64 steps (the opening button, then each step it records)
- **When** the engine applies it
- **Then** it answers `400` before it replays anything, as for a malformed `_prompt`, while 64 steps are replayed: each step is a render and a press on the engine's one thread, so a forged post of thousands of steps would hold it for minutes (the security audit's F2).

## Element binding

### ENGINE-96: Unbound element properties are written as rendered

- **Given**
  - Bindings to another element's property that is not bound (`{Binding #slider.Value}` on a `ProgressBar`, `{Binding Value, ElementName=slider}` on a text, `{Binding #slider.Value, StringFormat=...}` on a `Label`'s content, `{Binding #active.IsChecked}` on a class, `{Binding !#active.IsChecked}` on a visibility and on a button's `IsEnabled`, a `SplitView.IsPaneOpen` bound two-way to a toggle and read by a visibility, a `ProgressBar`'s `Minimum` and `Maximum` that no writer reads, an `Expander.ExpandDirection` bound to an unbound `ComboBox`'s selected item, and `$parent[ItemsControl].IsEnabled` in item templates, where the `ItemsControl` binds its items and its `IsEnabled` keeps its default or is bound to an unbound check box, as Semi's `DataGrid` theme reads `$parent[DataGrid].IsEnabled`; as Semi's and Ursa's demos bind a page's controls to each other)
- **When** the page is compiled
- **Then** the plan is reusable and each render, also after the plans file, writes what Avalonia rendered whatever the model: an element that reads no model data (or another such element through its own bindings), or a styled property that is not inherited and keeps its default or is bound to such an element, holds the same value on every render (a post that changes it goes through the Avalonia engine, since its field is not described)
- **When** a binding to a property of an element that reads the model (`{Binding #pick.SelectedItem}` on a `ComboBox` whose `ItemsSource` is bound)
- **Then** the plan is not reusable and the reason names the binding and the element's bound property.

*Precedent: ENGINE-60, which takes a source that is not the page's data at compile; Avalonia resolves `#name` once to the element (`ElementNameNode`) and reads its property like any other.*

### ENGINE-97: Bound content is written as its text

- **Given** content controls written through their templates (a `ContentControl`, and a `HeaderedContentControl` subclass with a header, as AdvancedToDoList's `LabeledControl`) whose `Content` is bound to a value that is not a control and that no data template types (a number, a date, a date with `TargetNullValue='Not yet completed'`)
- **When** the page is compiled
- **Then** the plan is reusable and each render, also after the plans file, writes the value's text as Avalonia's default data template shows it, and the `TargetNullValue` text for a null value.

### ENGINE-98: Template multi bindings of constants are written as rendered

- **Given** a control template with a `MultiBinding` of `TemplateBinding`s (as Semi's `HeaderedContentControl` theme shows a separator only when both `Header` and `Content` are set) and a binding to the templated control's `Header` (`RelativeSource TemplatedParent`, `$templatedParent.Header`) through a converter (as Ursa's themes show a presenter only when its content is set), on templated controls whose `Header` and `Content` are set in the XAML, one of them an item declared in the XAML of an items control that is its own container (as Ursa's `FormGroup` in a `Form` and a `TabItem` in a `TabControl`)
- **When** the page is compiled
- **Then** the plan is reusable and writes what Avalonia renders whatever the model, since the templated control holds the same values on every render
- **When** the same control with its `Content` bound to the model
- **Then** the plan is not reusable and the reason names the `MultiBinding`.

### ENGINE-329: A bound track follows the part it spaces

- **Given** a grid whose spacing rows' heights are bound to the visibility of the parts they follow (`RowDefinition.Height` bound through a converter, `Source` the part, to its `IsVisible`, itself bound to the model), as Aioros.Cosmo's field spaces its parts
- **When** the page is planned and rendered for a model that shows the part and one that hides it
- **Then** the plan writes each bound track from the model as Avalonia lays it out (the grid's class reads `var(--rN)` and its inline style writes the track), so a hidden part leaves no gap on the web as it leaves none in Avalonia; a binding with an element as its `Source` reads that element's property as `#name.Property` reads it.

## Stream binding

### ENGINE-120: Stream bindings show the latest value

- **Given** bindings through Avalonia's stream operator (`{Binding Title^}` on a text and, through a converter, on a visibility, `{Binding Note^}` on a text, where `Title` is an `IObservable<string?>` that gives its current value when subscribed, as Irihi.Lingua's localized strings and Ursa's demo do, and `Note` a completed `Task<string>`)
- **When** the page is compiled
- **Then** the plan is reusable and each render, also after the plans file, writes the value Avalonia shows (the observable's latest value, the task's result) for every model, and the stream is left unsubscribed
- **When** an observable that gives no value when subscribed at compile (Avalonia then leaves each property at its default and runs no converter)
- **Then** the plan is not reusable and the reason names the stream binding.

*Precedent: Avalonia's `StreamNode` and its `ObservableStreamPlugin` and `TaskStreamPlugin`.*

## Template selector

### ENGINE-131: Selectors that build in code are learned for each value

- **Given** a `ContentControl` whose bound content is an enum (or a `bool`) and whose `ContentTemplate` is an app template selector that builds its controls in code (a class implementing `IDataTemplate` with `Match` testing the type and `Build` switching over the value to create a `PathIcon`, returning nothing for a value it does not draw, as AdvancedToDoList's `ToDoItemPriorityIconTemplateSelector` and `ToDoItemStatusTemplateSelector` given by `x:Static`), also with typed `DataTemplate`s of other types in the application
- **When** the page is compiled
- **Then** the plan is reusable and each render writes the content as Avalonia renders it for every value, each value's case learned by building the selector on it, also after the plans file, and no app code runs at request time; a selector of this kind over content of a type with no finite set of values (a class) leaves the plan not reusable with a reason that names the selector, and the page is served by the engine.

*Precedent: Avalonia's `ContentPresenter` asks `ContentTemplate.Match` before the hosts' `DataTemplates`; ENGINE-26 learns one case per typed template the same way.*

### ENGINE-132: Selectors of keyed templates pick their template on each render

- **Given** an `ItemsControl` whose `ItemTemplate`, or a `ContentControl` whose `ContentTemplate`, is an app template selector of the documented shape (a `[Content] Dictionary<string, IDataTemplate>` of `DataTemplate`s by `x:Key` and a `DefaultTemplate` property, `Match` testing the item's type and `Build` picking a template by a property of the item, as Avalonia's "How to implement a data template selector" and AdvancedToDoList's `HamburgerMenuItemContentTemplateSelector` by type)
- **When** the page is compiled
- **Then** the plan is reusable, each template the selector holds is learned from an item the selector gives it, and each render writes every item with the template the selector picks for it, for any mix and order of items, also after the plans file, where the selector is rebuilt from its type with its templates' keys and runs its own `Match` and `Build` (an item it does not match writes nothing); a selector whose template has no item to learn it, or that is not of this shape (no parameterless constructor, other settings, keys other than text, enums, numbers and types), leaves the plan not reusable with a reason that names the selector.

*Precedent: MAUI's `DataTemplateSelector` and WinUI's `DataTemplateSelector` call the app's selector at run time and map the template it returns to one built once; Avalonia's `ContentPresenter.FindDataTemplate`.*

## Rooted styles

### ENGINE-158: Bound box values on controls are inline styles

- **Given** controls written by their own writer (a `Button`, a `CheckBox`, a `TextBox`, a `Slider`, a `ProgressBar`, a `DatePicker`) with a bound `Width`, `Height`, `Margin`, `Opacity` or single transform, and a bound `Foreground` or `FontSize` on a button whose content is text
- **When** the page is compiled
- **Then** the plan is reusable and each render writes the model's value in the control's element's inline style, the same HTML (classes included) as a page compiled with that model, also after the plans file; given a bound `Foreground` on a button whose content is a control (whose text writes its own color), the plan is not reusable and its failure names `Button.Foreground`.

*Precedent: containers, text and icons (ENGINE-32, 36, 37), now through one shared root writer.*

### ENGINE-159: Bound frames on controls are inline styles

- **Given** a bound `Background`, `Padding` or `CornerRadius` on a control whose element is its frame (a `Button`, a `ToggleButton`, a `TextBox`, a `ComboBox`, except a `ComboBox`'s `Padding`, from which its height is computed)
- **When** the page is compiled
- **Then** the plan is reusable and each render writes the model's value in the element's inline style (`background`, `padding` and `border-radius` in px), the same HTML as a page compiled with that model, also after the plans file; given a bound `Padding` on a control whose element is not its frame (a `CheckBox`, whose padding Avalonia puts between its box and its text), the plan is not reusable and its failure names `CheckBox.Padding`.

## Plan reach

### ENGINE-160: Hidden controls are not checked

- **Given** a control hidden by a constant `IsVisible="False"` (or `{a:OnPlatform Default=True, Web=False}` on the web) whose own properties are bound in a way no plan follows (a bound `MaxWidth`)
- **When** the page is planned
- **Then** the plan is reusable and writes nothing for it, as Avalonia's render writes nothing for it; the same control visible still fails the plan naming that property, and a control hidden at compile by a style its bound class turns on (`Classes.gone="{Binding !Show}"`) is still written for the models that show it.

### ENGINE-161: Presenters show their controls bound values

- **Given** a templated control whose template's presenter shows one of the control's properties (`{TemplateBinding Header}`, `{Binding Icon, RelativeSource={RelativeSource TemplatedParent}}`, as Ursa's `NavMenuItem` shows the header and icon the menu's `HeaderBinding` and `IconBinding` bind) with a data template, and the page binds that property to a model path
- **When** the page is planned
- **Then** the plan is reusable and writes the presenter and what its template builds in the scope of that value, and nothing of the template's content when the value is null, byte for byte as Avalonia's render of models with another value and with none; the same property bound to a value that is null in the compiled model fails the plan, since no look can be learned from it.

### ENGINE-163: Multi bindings of constant templated properties are written as rendered

- **Given** a control template whose part is bound with a `MultiBinding` of `{Binding X, RelativeSource={RelativeSource TemplatedParent}}` children, one through a converter (as Ursa's `Badge` shows its badge when `Header` is set or `Dot` is true), on templated controls the page places with constant properties
- **When** the page is planned
- **Then** the plan is reusable and writes each control as Avalonia renders it (the badge of a control with a header shown, the one of a control with neither hidden).

### ENGINE-165: Items that are constants are written as rendered

- **Given** a list whose items are constants of the page (an `x:Static` list of texts or of values such as numbers or colors, set in the XAML, as the palette Semi's `ColorView` template lists), whose item template binds a tip through a converter to the item (`ToolTip.Tip="{Binding Converter=...}"`, as Semi's palette names each color with `ColorToDisplayNameConverter`)
- **When** the page is planned
- **Then** the plan is reusable and writes each item and its tip as Avalonia renders them, since the bindings read constants and no model; the same template over a list bound to the model is planned as a repeat.

### ENGINE-178: Templates reading their own constants are written as rendered

- **Given** a `ColorView` the page sets constant properties on (`Color="#0D6EFD"`), whose template binds parts to its own properties through `$templatedParent` (`HsvColor`), parts to other parts' properties its code sets (`Minimum="{Binding #Component1Slider.Minimum}"`, `Height="{Binding #AlphaComponentNumericUpDown.Bounds.Height}"`), visibilities to `MultiBinding`s of `TemplateBinding`s, and palette items whose brush binds its color to its item
- **When** the page is planned
- **Then** the plan is reusable and writes the view byte for byte as Avalonia renders it, since every one of those bindings reads constants of the page and no model.

### ENGINE-179: Style bindings to constant elements are constants

- **Given** a templated control whose properties a page style sets to bindings of named elements that hold constants of the page (`<Setter Property="IsAlphaVisible" Value="{Binding #alpha.IsChecked}" />` for a `ColorView`, as Semi's color picker page drives its views from check boxes), read by its template's `MultiBinding`s of `TemplateBinding`s
- **When** the page is planned
- **Then** the plan is reusable and writes the control byte for byte as Avalonia renders it; the same setter bound to an element whose own value reads the model still fails the plan.

## Learned plan

### ENGINE-162: Geometries from converters are learned per value

- **Given** controls in a list's item template whose `Geometry` property is bound through a converter (`PathIcon.Data="{Binding Name, Converter=...}"`, as Ursa's demo turns each menu item's name into a `StreamGeometry`, which no plan slot can write)
- **When** the page is planned
- **Then** the plan is reusable and writes the control once for each distinct value of the binding's source the compiled page holds, as Avalonia renders the geometry the converter gave it, and each render writes the case of its item's value, byte for byte as Avalonia's render of models that reorder and repeat those values; a value the compiled page did not hold throws a `PlanMissException` naming it, from the plan and from the plan read back from a plans file.

### ENGINE-164: Theme variant scopes are written for each variant

- **Given** a `ThemeVariantScope` whose `RequestedThemeVariant` is bound to a model's `ThemeVariant?` (as Semi's demo previews its controls in a variant picked from a list)
- **When** the page is planned
- **Then** the plan is reusable and writes the scope once for each variant the app can show (none, `Default`, `Light`, `Dark`, and every variant a theme dictionary of the application's styles and resources is keyed by, such as Semi's `Aquatic`), as Avalonia renders each, picking the model's variant by its key on each render, byte for byte as Avalonia's render; a variant the app does not declare throws a `PlanMissException`.

### ENGINE-308: Plain values bound through converters are planned

- **Given** controls whose plain values (a flag, a text, a number, an enum, edges, corners) are bound through converters the plans file can rebuild, on properties the plan never converted before (a check box's `IsChecked` bound to an enum, a border's `CornerRadius` bound to a flag)
- **When** the page is planned (decision "Deterministic plan coverage")
- **Then** the plan is reusable and writes them byte for byte as Avalonia renders models it was not compiled with, also read back from a plans file, and a post of the check box through the plan writes the converter's `ConvertBack` of its value to the bound enum, as the engine does; a value whose writing depends on measuring it (a geometry, ENGINE-162) is not run this way.

## Library control

### ENGINE-175: Own values shown by code are learned

- **Given** a templated control of a library (not Avalonia's, with no writer of its own, written through its template) whose own properties are bound to model paths and shown only by its code (a `double` its code turns into a text, as Ursa's `Rating.Value` sets its stars and `TimeBox.Time` its fields; a list of texts, as `TagInput.Tags`; a title its template shows as a text is learned as well, since its code may read it too), or a property it inherits from Avalonia that a part of its template reads in a `MultiBinding` (`Tag`, as Ursa's `Badge` shows its badge when `Header` is set)
- **When** the page is planned
- **Then** the plan is reusable and writes the control as Avalonia renders it, learned by the values the compiled model holds (a list by its items), byte for byte as Avalonia's render of the same values; a render with another value throws a `PlanMissException` naming it, so the engine renders it; the same property bound to a value with no key (a model object) still fails the plan naming the property.

### ENGINE-176: Template bound values without keys are slots

- **Given** a templated control of a library whose template shows, through a `TemplateBinding`, a property it inherits from Avalonia (`IsTabStop` as a part's `IsVisible`) or one of its own whose value has no key (a list of model objects as an items control's `ItemsSource`, as the demo's `AnchorScrollViewer` lists its `AnchorItems`), both bound to the model
- **When** the page is planned
- **Then** the plan writes that visibility as a condition and that list as a repeat of the model's path, with no case learned: renders of models with other lists and the flag set or not are byte for byte Avalonia's.

### ENGINE-177: Library lists are learned with their items

- **Given** a library's list control (an `ItemsControl` of its own) whose items are bound to a list of texts and whose own property, bound to the model, changes each item's look in its code (the chosen item opaque, the others half transparent, as Ursa's `SelectionList` marks its selected item)
- **When** the page is planned
- **Then** the plan is reusable and writes every item as Avalonia renders it, learned with the list's items and that property, byte for byte as Avalonia's render of the same values, and a render with other items throws a `PlanMissException`; the same list with no other bound property is still planned as a repeat, writing lists it never saw as Avalonia does.

### ENGINE-180: Template bound brushes in lists are inline styles

- **Given** templated controls of a library in a bound list's item template (as Semi's palette lists its `ColorItemControl`s) whose `Background`, `Foreground` and title are bound to the item and shown by their template's parts through `TemplateBinding`s (a border's background, a text's color and text)
- **When** the page is planned
- **Then** the plan is reusable and writes each item's background and text color as inline styles and its title as text, from the item's values, byte for byte as Avalonia's render of lists with other colors and titles; a control whose template leaves a text to inherit the bound color (an items control's default texts) fails the plan naming that color, since the inherited text would keep the compiled model's.

### ENGINE-181: Keyed indexer bindings are refused

- **Given** a binding through an indexer that is not an integer index of a list (a compiled `{Binding Lookup[red].Name}` on a `Dictionary<string, T>`)
- **When** the page is planned
- **Then** the plan fails naming the binding and its indexer; the page is rendered by the engine.

### ENGINE-212: Bound library flags are planned for every value

- **Given** a library's panel (not a templated control, whose own properties are learned by value, ENGINE-175, nor a control such as an overlay whose flag opens something, which is not toggled while the page is written) with a `bool` styled property of its own that sets a pseudo-class (`Flagged.IsInvalid` setting `:invalid`, as Aioros.Cosmo's `Field.IsInvalid`), bound to the model, and a style that colors its texts under that pseudo-class
- **When** the page is planned and rendered for both values
- **Then** the plan is reusable and writes the control once per value (false and true), each as Avalonia renders it, choosing the model's at render time, as a bound enum or class is, instead of failing on a binding the plan does not follow.

### ENGINE-217: Item states by position are planned

- **Given** a library `ItemsControl` whose `PrepareContainerForItemOverride` marks its first container (as Aioros.Cosmo's `c:Breadcrumb` sets `IsFirst`, which hides the separator its container's template shows before the item) and an `ItemsControl` whose style makes its last item's text bold (`ContentPresenter:nth-last-child(1)`), both bound to the model's list
- **When** the page is compiled from a list of three items and rendered for lists of one, two, three and five
- **Then**
  - The plan is reusable and each render, also after the plans file, writes what Avalonia's render writes: the separator before every item but the first, the last item's text bold, a single item first and last at once.
  - The plan learns the item's body at each position (first, middle, last, only) from stand-in lists of the traced item when the list is a library's or a `:nth-` style is in its scope, and repeats one body when the four are the same.

*Precedent: CSS `:first-child`, `:last-child` and `:only-child`, and the plan's variants learned per value (`ChoiceNode`), here per position as Avalonia's render shows them.*

### ENGINE-268: Values a library writes back are learned as a fresh model holds them

- **Given** a library's templated control (`Defaulted`, shaped as Aioros.Cosmo's `c:Command`, which selects its first item while it is written) whose own `Choice` is bound two way to the model's `Choice` and whose code writes its default (`first`) into it on its first render when it is null, so that the model the page is compiled with holds `first` once the page is written
- **When** the page is planned for a model whose `Choice` is null
- **Then**
  - The plan is reusable and learned by the value the model held before the render as well as by the one the render wrote back: a fresh model (null) and a model holding `first` both render from the plan, byte for byte as Avalonia renders them (both show `first`), while a model holding `second` is a `PlanMissException`; the values are read from the page's models before they are bound, and the precompilation loads the page's models anew for each theme it compiles, so neither theme depends on the models another compile wrote into.
  - Avalonia's engine runs the library's code on every request; the plan must answer what a fresh request's render answers.

## Library command

### ENGINE-191: Library commands are learned by their state

- **Given** a templated control of a library (not Avalonia's, with no writer of its own) whose own command property is bound to a model command that its code runs itself (`ConfirmCommand`, as Ursa's `PopConfirm.ConfirmCommand` and `PathPicker.Command` are run on a confirmation or a picked file) and whose look may follow whether that command can run
- **When** the page is compiled
- **Then** the plan is reusable and learns the control by its command's state: each render with a command set that can run, as the compiled model's, writes the control as Avalonia renders it, and a render whose command is missing or cannot run with the control's parameter (a `<Name>Parameter` property, unset, a text or bound to a model path) is a plan miss (`PlanMissException`) served by the engine
- **When** the parameter bound to something the plan cannot follow (`ConfirmCommandParameter="{Binding $self.Tag}"`, as Ursa's `Pagination.CommandParameter` reads its own `CurrentPage`)
- **Then** the plan is not reusable and the reason names the parameter.

*Precedent: Avalonia's `ICommandSource` controls, whose look follows `CanExecute` (ENGINE-28's enabled states), and ENGINE-121, which writes a control that runs its own code as rendered.*

### ENGINE-192: Library commands post through the engine

- **Given** the control of ENGINE-191, holding a button of the page (`<m:Confirm><Button Content="Save" /></m:Confirm>`, as `PopConfirm` holds the button that opens it) or of its template
- **When** the page is compiled and that button is posted
- **Then** no field of the control is described to the plan, so the post goes through the Avalonia engine, which clicks the button and lets the control run the model's command with its parameter, as Avalonia does on the desktop (the plan cannot run a control's code); the fields outside the control are described as before.

## Slot writer

### ENGINE-182: A bound value is written by the library writer per render

- **Given** a page with a library control written by a slot writer (`IWrittenBySlot`) whose value property is bound to a list of the view model
- **When** the page is planned
- **Then** the control is written as its box (a `<div>` with its layout class and its accessible name) around a writer node that reads the binding's path, the plan has no failure, and rendering the plan for a model with other data writes the writer's output for that data, the same bytes the engine writes for that model.

### ENGINE-183: An unbound value is a literal like the engines

- **Given** the same control with its value set in the XAML (no binding)
- **When** the page is planned
- **Then** the writer's output for that value is a literal of the plan, identical to the engine's render, and no writer node is added.

### ENGINE-184: Unfollowed bindings fail the plan

- **Given** the same control with its value bound through a path the plan cannot follow (a dictionary's key), or with another of its properties bound (one the writer does not read)
- **When** the page is planned
- **Then** the plan fails with a reason naming the binding, and the page is served by the engine.

### ENGINE-193: Bound parts of children are followed by the plan

- **Given** a library control written by a slot renderer (`ISlotRenderer`, declared with `WrittenSlot.Rendered<T>`) whose value property is bound to a list and that declares reads (`SlotPart.Read`) of a property of a child it holds in a styled property (a line's `Stroke` in an Avalonia list, bound to the model) and of a child set in the XAML (not bound)
- **When** the page is planned
- **Then** the plan has no failure, its writer node reads the value's path and the bound child's path, and rendering the plan for a model with other data and another bound value writes the renderer's output for those values, where the renderer finds the bound read under its key (`SlotOutput.TryRead`) and no read under the unbound one's, the same bytes the engine writes for that model, also after the plan is saved and read back.

*Precedent: Recharts' chart reading its children's props (`findAllByType` over a chart's children in `generateCategoricalChart`); React's and Lit's components receiving each bound prop as its own hole.*

### ENGINE-194: The engine hands the renderer the bound parts

- **Given** the page of ENGINE-193 with another property of the control bound that the renderer does not declare, so the plan fails
- **When** the page is rendered by the engine for a model
- **Then** the renderer finds the bound child's value under its key, as the engine's binding holds it for that model, and writes the same bytes the plan writes for that model in ENGINE-193.

### ENGINE-195: Parts the plan cannot follow fail it

- **Given** the control of ENGINE-193 with a declared read bound through a path the plan cannot follow (a dictionary's key), or with its value set in the XAML while a declared read is bound, or with a part naming a property its target does not have
- **When** the page is planned
- **Then** the plan fails with a reason that names the control's or the child's property, and the page is served by the engine.

### ENGINE-196: A posted field is written back and rendered

- **Given** a slot renderer that declares a number field (`SlotPart.Number`, with a minimum and a maximum) on a child's property bound two-way to the model and writes it as a range input (`SlotOutput.WriteField`)
- **When** the page is planned
- **Then** the input is written with ` name="f{id}" data-change` (the field's id numbered among the page's fields, and the attribute that makes the framework script post the form when it changes, LiveView's `phx-change`), the plan describes the field, and a post that names the field's token in `_chg` with a new value, through the plan and through the engine, writes that number into the model, so the page rendered after the post shows the renderer's output for the new value.

*Precedent: ASP.NET Web Forms' `IPostBackDataHandler`, a control declaring the posted fields it loads; LiveView's `phx-change`.*

### ENGINE-197: Posted fields outside their numbers are kept or clamped

- **Given** the field of ENGINE-196
- **When** a post names a value that is not a number, a number outside the field's range, or no value for the field, then, through the plan and through the engine alike, a value that is not a number and a missing value leave the model as it was and a number outside the range is clamped to it; given a renderer that writes a field under a key its control did not declare as a field, when the page is compiled
- **Then** compiling throws an error naming the key.

### ENGINE-198: A posted command runs with its item

- **Given** a slot renderer that declares a command (`SlotPart.Command`) on the control's command property, bound to a command of the model, and writes one submit button per item with the item's index as its parameter (`SlotOutput.WriteCommand`)
- **When** the page is planned
- **Then** each button is written with ` type="submit" name="_btn" value="{id}|{index}"`, the plan describes each as a command field, and a post of one item's button, through the plan and through the engine, runs the model's command once with that item's index (as text, as a XAML literal parameter is), and the page rendered after the post shows what the command changed.

*Precedent: ASP.NET Web Forms' `IPostBackEventHandler` and `GetPostBackEventReference` (a control's markup posting back an event argument, as a grid's `Select$3`).*

### ENGINE-199: Commands posted without their button run nothing

- **Given** the buttons of ENGINE-198
- **When** a post names a button's token with a parameter no button of the render was written with, or a button token the render does not write (buttons are numbered apart from fields since ENGINE-210, so a field's token names no button)
- **Then** the post is refused as a conflict and no command runs, through the plan and through the engine alike.

### ENGINE-200: A choice field is one name over its options

- **Given** a slot renderer that declares a choice field (`SlotPart.Choice`) on a child's text property bound two-way to the model and writes it as two radio inputs and a submit button (`SlotOutput.WriteFieldOption` on each)
- **When** the page is planned
- **Then** Avalonia's render and the plan agree (also once saved and read back), every option is written with the same ` name="f{id}"`, its own ` value` and ` data-change`, the field takes one id (the next field is numbered after it), and the plan describes one `choice` field on the bound path with the values of its options
- **When** a post names one option's value with `_chg`, through the plan and through the engine alike
- **Then** the bound property takes that value and the page rendered after the post marks that option.

*Precedent: HTML's radio group (one name, a value per input) and a submit button's name and value (the button posts its own value, as Web Forms' `RadioButtonList` and a set of buttons choosing one value do); LiveView's `phx-change`.*

### ENGINE-201: Choices take only their options

- **Given** the choice of ENGINE-200
- **When** a post names a value that no option was written with, or no value for the choice, then, through the plan and through the engine alike, the property keeps its value; given the same choice bound one way, when the page is planned
- **Then** the choice is written undescribed, so the plan does not bind its posts and leaves them to the engine.

### ENGINE-202: A composite field moves its members together

- **Given** a slot renderer that declares two number fields on a child's properties bound two-way (`from` and `to`) and a composite field over them (`SlotPart.Composite("slide", "from", "to")`), writes the composite as one range input (`SlotOutput.WriteField`) and decodes a posted slide (`ISlotRenderer.Decode`) into the new start and the start plus the current span
- **When** the page is planned
- **Then** the plan describes the composite with both members' fields
- **When** a post names a new slide with `_chg`, through the plan and through the engine alike
- **Then** both bound properties move by the same amount and the page rendered after the post shows the new window.

*Precedent: WPF's and Avalonia's `IMultiValueConverter.ConvertBack` (one target value split back into several sources).*

### ENGINE-203: Composites refused or out of range are kept or clamped

- **Given** the composite of ENGINE-202
- **When** a post names a slide the decoder refuses (not a number), then, through the plan and through the engine alike, both properties keep their values, and when the decoded end falls outside its field's range
- **Then** it is clamped as the field's own post would be
- **When** a composite naming a key that is not a number or text field the control declares, when the page is compiled
- **Then** compiling fails with an error naming the composite
- **When** a composite whose member is bound one way, when the page is planned
- **Then** the composite is written undescribed and its posts are left to the engine.

### ENGINE-204: A button the bound post writes runs

Given a slot renderer whose buttons depend on a field the post carries (one command button per item from the window's start, numbered after the start's field), when a post moves the start and presses a button that only the render of the bound post writes (the page the browser showed), not the render before binding, then, through the plan and through the engine alike, the button's command runs with its item and the post is answered.

*Precedent: ASP.NET Web Forms, which loads the posted data into its controls before it raises a postback event and checks the event against the controls as loaded (`RaisePostBackEvent` after `LoadPostData`).*

### ENGINE-205: A button no render writes is a conflict after binding

Given the page of ENGINE-204, when a post moves the start and names a button token with a parameter that neither the render before binding nor the render of the bound post writes, then, through the plan and through the engine alike, the post is refused as a conflict and no command runs; a view that holds no slot renderer still refuses a button that is not on the page before anything is bound (ENGINE-52).

### ENGINE-206: A refused post leaves the bound fields as they were

Given the page of ENGINE-204 over models that outlive the request, when a post moves the start and names a button that neither the render before binding nor the render of the bound post writes, then, through the plan and through the engine alike, the post is refused as a conflict and the start keeps the value it had before the post: only the slot renderer's fields are bound to look for the button in the render of the bound post, and they are put back when it is not there, before any other field is bound.

*Precedent: a database transaction rolled back when a check fails after the first writes; ASP.NET Web Forms checks the event target against the controls as loaded (ENGINE-204).*

### ENGINE-208: A number field is clamped to its propertys type

- **Given** a slot renderer that declares a number field over a `byte` property with a range wider than a byte (0 to `int.MaxValue`), bound two-way to the model
- **When** a post names `300` for it, through the plan and through the engine alike
- **Then** the property takes `255`, clamped to its type as well as to the declared range, and the post is answered instead of failing with an overflow.

### ENGINE-209: A stale composite beside the changed field is not decoded

Given the composite of ENGINE-202 over a window from 1 to 3, when a post's `_chg` names the start's own field (`f0=3`) and carries the composite's value as the page last wrote it (`f1=1`, stale beside the changed field), then, through the plan and through the engine alike, the composite is not decoded: the start takes 3 and the end keeps 3; a composite is decoded only when `_chg` names it or the post names no changed field (ENGINE-202).

*Precedent: LiveView's `phx-change`, whose `_target` names the input that changed, which handlers read to apply only that input.*

### ENGINE-210: Buttons are numbered apart from fields

- **Given** a slot renderer that writes one command button per item from its window's start (three buttons from `0`, one from `2`) followed by a text box bound to the model
- **When** the page is rendered from either start, then, through the plan and through the engine alike, the buttons are numbered apart from the fields (`_btn` values `0|…`, `1|…` in the buttons' own sequence) and the text box keeps the same name (`f1`, after the start's `f0`) whatever the number of buttons; and when a post moves the start to `2`, renames the text box and presses the button that only the bound render writes (`0|2`)
- **Then** the button's command runs with `2` and the text box's value is bound, while a button not on the page with that parameter is still a conflict checked before anything stays bound (ENGINE-205 to 207).

*Precedent: ASP.NET Web Forms, whose postback event target is named apart from the posted data (`__EVENTTARGET` beside the inputs' names); LiveView, whose events (`phx-click` with `phx-value-*`) are named apart from a form's inputs.*

## State class

### ENGINE-211: Bound classes keep one state class in item templates

- **Given** an item template whose `Border` has a bound class (`Classes.on="{Binding On}"`), a `:pointerover` style for each of its looks and `a:Web.Href`, as Aioros.Cosmo's theme chips
- **When** the page is compiled
- **Then** the plan is reusable and writes, for every model, what Avalonia renders: each item's element carries the one state class of its look once, never the state classes of the other variant or the same class twice.

## Dialog post

### ENGINE-213: A dialog opened by a command that ended is a prompt

- **Given** a dialog control whose bound `IsOpen` adds a mask and a surface to a host panel of the page, and a button whose command only sets the open state and ends (no command left waiting)
- **When** the button is posted
- **Then** the answer writes the dialog as a prompt whose `_prompt` field names that button
- **When** the dialog's closing button is posted with it
- **Then** the engine replays the opening and presses the closing button, and the answer has no prompt.

### ENGINE-214: Escape and a click outside are handed to the dialog

- **Given** a dialog left open as a prompt whose surface closes it on the Escape key and whose mask closes it on a press while light dismiss is on
- **When** the prompt posts `_dismiss=escape`
- **Then** the engine replays its steps and raises Escape where Avalonia's focus is in the surface (the surface when nothing there has it), and the answer has no prompt
- **When** it posts `_dismiss=outside`
- **Then** the mask is pressed: the dialog closes, and a dialog without light dismiss stays open and is answered again as a prompt with its steps.

### ENGINE-215: A prompt is labelled and laid out by its surface

- **Given** a prompt whose surface is centered with a `MaxWidth` of 425 and holds a semibold title, a description beside it and a text box, and a second prompt whose surface announces assertively (`AutomationProperties.LiveSetting="Assertive"`) with a cancel button (`IsCancel`)
- **When** they are written
- **Then** the first `<dialog>` is `aria-labelledby` its title's element (`id="prompt1-title"`) and `aria-describedby` its description's, its mask has the class `pm`, its surface sits by its alignment (`justify-self:center;align-self:center`) within the host's box (`max-width:100%;max-height:100%`, scrolling) rather than at the offsets Avalonia rounded, and nothing in it has `autofocus` (the browser focuses its first control, as Avalonia's dialogs do, and the control Avalonia focused would take it); the second is `role="alertdialog"` with `autofocus` on its cancel button, and no part of it has a `role="alert"` of its own (SEO-43), since the dialog's role announces it.

### ENGINE-216: A notices button posts its command

- **Given** a command that shows a notice holding a button with a command (a toast's action)
- **When** it is posted
- **Then** the notice carries the step that showed it (`data-replay`)
- **When** that button is posted with it in `_prompt`
- **Then** the engine replays the step, presses the notice's button so its command runs once, and the answer does not write the replayed step's notice again.

### ENGINE-271: A notices button posts its command without the script

- **Given** a command that shows a notice holding a button with a command (a toast's action)
- **When** the answer is written
- **Then** the notice carries a hidden `_notice` field with that button's token and the step that showed it; and when the answer's form is posted as a browser without scripting posts it (every field of the page, `_notice` among them, and the pressed button), the notice's button runs its command through the replayed step (ENGINE-216), while a button of the page runs its own command with no step replayed.

## Library property

### ENGINE-218: Shown library properties are slots

- **Given** a library's templated control (`Callout`, shaped as Aioros.Cosmo's `c:Alert` and `c:Item`) whose own `object` properties `Title` and `Description` are bound to the model's texts and shown only by its template's presenters (`Content="{TemplateBinding Title}"`, visible through `{TemplateBinding Title, Converter={x:Static ObjectConverters.IsNotNull}}`)
- **When** the page is planned and rendered, also after the plans file, for other texts and for a null description
- **Then** the plan is reusable and each render writes Avalonia's, with no `PlanMissException`: the properties are slots of their bindings, as a `TemplateBinding` is a one-way read (WPF, Avalonia), kept because a second trace with another value set above each binding records the same plan; a library property its code reads (`Gauge.Level`, ENGINE-175) still records another plan and stays learned by value.

### ENGINE-219: Library properties in list items are slots

- **Given** a bound list whose item template holds library controls with their own properties bound to the item (`Callout.Title` and `Callout.Description`, and a `Portrait`, shaped as Aioros.Cosmo's `c:Avatar`, whose bound `Content` shows the item's initials in a presenter that is not its `PART_ContentPresenter`)
- **When** the page is planned and rendered for lists of other items, one of them without a description
- **Then** the plan is reusable and each item is written with its own values as Avalonia renders it, instead of failing with `Callout.Title is bound, and the plan does not follow it`.

### ENGINE-220: Library decorator values are learned

- **Given** a library's decorator (`Proportion`, shaped as Aioros.Cosmo's `c:AspectRatio`, not templated) whose own `double` property sizing its child is bound to the model
- **When** the page is planned from one value and rendered for it and for another
- **Then** the plan is reusable, writes the first as Avalonia renders it and throws `PlanMissException` for the other, which the engine then serves, as a templated control's own keyed values are learned (ENGINE-175), instead of failing with `Proportion.Value is bound, and the plan does not follow it`.

### ENGINE-221: Bound open flags of flyouts are planned

- **Given** a button with a flyout whose open state is a library's attached `bool` bound to the model (`Pop.IsOpen`, shaped as Aioros.Cosmo's `c:Popover.IsOpen`)
- **When** the page is planned from a closed model and rendered for a closed and an open one
- **Then** the plan is reusable and writes each as Avalonia renders it: the flyout's popover is the same in both, and carries `data-aioros-open` only while the flyout is open (SSR-198), a flyout open when the page is written being written from a presenter realized for it, as a closed one is, when its own popup has none; before, the page failed with `Button.IsOpen is bound, and the plan does not follow it`.

### ENGINE-285: Bound flags of library buttons are planned

- **Given** a library's button whose own `bool` flag only styles it (`Tab.IsActive` setting `:active`, shaped as Aioros.Cosmo's `SidebarMenuButton.IsActive`) bound to the model
- **When** the page is planned from an inactive model and rendered for an inactive and an active one
- **Then** the plan is reusable, writes what Avalonia renders for each and the two differ by the `:active` look.

### ENGINE-286: Flyouts open from the start are planned

- **Given** a button whose library open flag (`Pop.IsOpen`, which opens its flyout once the button is loaded, as Aioros.Cosmo's `c:Popover.IsOpen` does when it is set before the flyout) is bound true
- **When** the page is planned from that open model and rendered for an open and a closed one
- **Then** the plan is reusable (the closed flyout is written with the presenter it was open in, which keeps its content, as an open one is) and only the open one carries `data-aioros-open`.

### ENGINE-288: Controls hidden in the first item are planned as they show

- **Given** a list whose item template shows a text for assigned items and a 152 px select for the others (`IsVisible` bound both ways, as Aioros.Cosmo's dashboard-01 reviewer cell)
- **When** the page is planned from a model whose first item hides the select and rendered for one whose items all show it
- **Then** the plan is reusable and writes what Avalonia renders for both: a control its compiled item hides is written shown and laid out, so its look (its 32 px height) is the one it has when it shows.

### ENGINE-292: Paths stroked with the foreground of a learned library control are planned

- **Given** a library control whose bound status styles its foreground (`Phase.Status`, an enum, setting `:running`, shaped as Aioros.Cosmo's `TraceLine.Status` bound to a streamed turn) holding in its icon a path whose stroke is bound to that foreground (`Stroke="{Binding $parent[m:Phase].Foreground}"`)
- **When** the page is compiled with the status `Done`
- **Then** the page compiles to a plan, and each status writes the path stroked in the foreground Avalonia gives the control for it (`#0a0a0a`, then `#737373`), also from the plans file.

### ENGINE-293: Paths stroked with the foreground of a button enabled by the model are planned

- **Given** a button enabled by the model (`IsEnabled="{Binding Idle}"` and a bound command, as Aioros.Cosmo's code block's Replay while a stream plays) holding an icon whose stroke is bound to the button's foreground (`$parent[Button].Foreground`)
- **When** the page is compiled idle
- **Then** the page compiles to a plan, and an idle and a busy turn each write the button and its icon as Avalonia renders them, the two differing by the disabled look, also from the plans file.

### ENGINE-294: Paths stroked with the foreground of a line shown by the model are planned

- **Given** a library line shown by the model (`IsVisible="{Binding Asked}"`) whose bound status styles its foreground, holding an icon stroked with that foreground
- **When** the page is compiled with the line hidden
- **Then** the page compiles to a plan, and a hidden, a shown and a shown running turn each write what Avalonia renders, the running line's icon stroked `#737373`, also from the plans file.

### ENGINE-295: Paths stroked with the foreground of a line with bound text are planned

- **Given** an `Expander` whose header is a library line (the app's content its template presents, so its templated parent is the expander) with a bound text and a bound status styling its foreground, holding an icon stroked with that foreground (as Aioros.Cosmo's reasoning row)
- **When** the page is compiled with `Thinking`
- **Then** the page compiles to a plan, the line's text is a slot, and a running turn titled `Thought` is written from the plan as Avalonia renders it, also from the plans file.

## Shell

### ENGINE-238: Library flags flip in the browser

- **Given** a library's panel marked `a:Web.Flag="IsOpen"`, naming a `bool` property of its own that its code turns into its children's pseudo-classes (Aioros.Cosmo's `c:SidebarProvider.IsOpen`, open), whose side is 240 px wide and 48 px under `:collapsed`, with a label hidden and an icon shown only under `:collapsed`, a button of the library's marked `a:Web.Flips="IsOpen"` with the hot key Ctrl+B, and a plain button after it
- **When** the page is planned
- **Then**
  - The plan is reusable and writes what Avalonia renders; the panel carries `data-aioros-flag="IsOpen"` and `data-aioros-on`; the side, the label and the icon are each written once, with a class whose rules give the side `width:240px` under the panel's `data-aioros-on` and `width:48px` without it, hide the label (`display:none`) without it and the icon with it; the flipping button is a `type="button"` with `data-aioros-flips="IsOpen"`, `aria-expanded="true"` (the WAI-ARIA disclosure pattern) and its `data-aioros-key`, not a field of the form, and the plain button keeps the number it has without the flag (`value="0|"`), as a button opening a flyout keeps its popover's.
  - A control the trace of the other value never met (a side that builds its content again whenever the flag changes, as a table realizes its rows again when its width changes) is written with its own look in both values, not hidden.
  - A panel whose `a:Web.Flag` names no `bool` property of its own is written as before, without `data-aioros-flag`.

### ENGINE-239: Bound library flags flip in the browser

- **Given** the panel of ENGINE-238 with its `IsOpen` bound to the model's `Open` (Aioros.Cosmo's Workspace binds `SidebarProvider.IsOpen` to `SidebarOpen`)
- **When** the page is planned with the flag open and rendered for a model with it open and one with it closed
- **Then** the plan is reusable and writes what Avalonia renders for both; the two renders differ only in the panel's `data-aioros-on` mark, the flipping buttons' `aria-expanded` and the check box that posts the flag (ENGINE-259), which follow the model, every control written in both with the same classes, so the browser switches the flag as when it is not bound; and the flag is neither written once per value (ENGINE-212) nor learned by value.

### ENGINE-259: Bound flags are posted

- **Given** a library panel whose flag (`a:Web.Flag`) is bound two way to the model (Aioros.Cosmo's Workspace binds `IsOpen` to `SidebarOpen`)
- **When** it is rendered for an open and a closed model
- **Then** the panel is marked `data-change` and holds a hidden check box (`<input type="checkbox" hidden name="f{id}">`), `checked` while the model's flag is, read from the model by the plan
- **When** a post carries the check box
- **Then** the model's flag is true, and when it leaves it out, false, through the plan and through the engine; a flag that is not bound two way writes neither the mark nor the check box.

*Precedent: the tabs' selected index posted through their hidden input (FOCUS-07); an HTML check box, posted only while checked; Phoenix LiveView's `phx-change`.*

### ENGINE-260: Tips of the other value are written

- **Given** a library panel's flag whose other value has the library set a tool tip on one of its controls (Aioros.Cosmo's icon sidebar sets its menu buttons' `ToolTip.Tip` only while collapsed)
- **When** the panel is rendered in either value
- **Then** the control is written with that tool tip (`role="tooltip"`, `aria-describedby`) as Avalonia shows it in the value where it is set, and the tip is hidden while the panel's mark is in the value where the library sets none, so it shows only once the browser switches the panel to the other; a compiled plan of a page whose flag is bound writes it as Avalonia's render does, on a button with a flyout too (a tip's popover id is numbered where it is first written, after the flyout's `popovertarget`, as the plan numbers popovers in document order).

*Precedent: shadcn/ui's `SidebarMenuButton`, whose `TooltipContent` is always rendered and `hidden` unless the sidebar is collapsed.*

### ENGINE-261: Flag cookies are named on their panel

- **Given** a library panel's flag with `a:Web.FlagCookie="sidebar_state"` (Aioros.Cosmo's `c:SidebarProvider.StateKey`) and one without
- **When** the page is rendered
- **Then** the first panel carries `data-aioros-cookie="sidebar_state"` beside its flag's marks, for the flag module to keep its state in that cookie, and the second carries none.

### ENGINE-262: Flags become sheets below their breakpoint

- **Given** a library panel's flag with `a:Web.Breakpoint="600"` whose side has `a:Web.Sheet="IsSheetOpen"` (Aioros.Cosmo's `c:Sidebar` and `c:SidebarProvider.IsMobileOpen`), the side 240 px wide in a wide window and full width as an open sheet in a narrow one, and a mask in the side shown only in the open sheet with `a:Web.Flips="IsOpen"`
- **When** the page is rendered
- **Then** the panel carries `data-aioros-breakpoint="600"`; the panel is traced a third time in a window narrower than the breakpoint with the sheet open, each look that differs there given under `@media (max-width:599.98px)` and the wide looks under `@media (min-width:600px)`; the side is wrapped in a `<dialog data-aioros-sheet>` that lays it out in its place above the breakpoint (`display:contents`) and covers the viewport when opened below it; the mask is written hidden above the breakpoint and shown below it, carrying `data-aioros-flips="IsOpen"`; the window is given its width back, and a compiled plan writes the page as Avalonia's render does.

### ENGINE-264: Nested library flags flip in the browser

- **Given** ENGINE-238's shell flag (`IsOpen`) whose side holds three library panels flagged `IsExpanded` (Aioros.Cosmo's collapsible `c:SidebarMenuItem`, whose code turns its own `IsExpanded` into `:expanded`), one expanded, one collapsed and one bound two way to the model, each with a button marked `a:Web.Flips="IsExpanded"` over a sub-list its theme hides while it is collapsed and the side's hides while the shell is closed, and the expanded one's sub-list holding a fourth such panel, collapsed, followed by a plain button
- **When** the page is planned
- **Then**
  - The plan is reusable and writes what Avalonia renders; each item carries its own `data-aioros-flag="IsExpanded"` (`data-aioros-on` while expanded) inside the shell's, and its button is a `type="button"` with `data-aioros-flips="IsExpanded"` and `aria-expanded`, not a field of the form (the plain button keeps `value="0|"`).
  - Each sub-list is written once, the collapsed item's written hidden, its class giving it its look under every combination of the marks around it.
  - Written as a chain of the panels from the outermost in (`:where([data-aioros-flag="IsOpen"][data-aioros-on]) :where([data-aioros-flag="IsExpanded"]:not([data-aioros-on])) .x.x`), so it shows only while the shell and every item around it are, whichever values the page was written with.
  - The bound item holds the hidden check box of ENGINE-259, which a post carries into the model.

*Precedent: shadcn/ui's sidebar, a Radix `Collapsible` per menu item (`data-state` on its own element) inside the `SidebarProvider`'s; the flag module already finds a button's nearest panel of its flag's name (`closest`).*

### ENGINE-282: An expander bound to a flag follows it in the browser

- **Given** a library panel flagged `a:Web.Flag="IsOpen"` and open, holding an `Expander` whose `IsExpanded` is bound two way to the panel's `IsOpen`, an `Expander` of its own and a button flipping the flag
- **When** the page is rendered
- **Then** the bound expander's `<details>` is open and marked `data-aioros-follows="IsOpen"` and `data-aioros-open-on="true"` (it is open in the flag's on value), with no `data-change` (its state is the panel's, switched in the browser), and the other expander carries no such mark.

*Precedent: Radix's controlled `Collapsible` (`open` and `onOpenChange` shared with a button outside it, shadcn/ui's `TraceOpen` example), Aioros's flags (ENGINE-238).*

## Overlay theme

### ENGINE-230: Item template buttons in a flyout run their command

- **Given** a button's `Flyout` whose content holds a button bound to the model's command and an `ItemsControl` of the model's links whose item template's `Button` binds its `Command` through `$parent[ItemsControl]`'s data context and its `CommandParameter` to the item (Aioros.Cosmo's navigation menu Gallery page)
- **When** the template's button is posted
- **Then** the command runs with the item's parameter, as the button outside the template runs its own: a button whose command binding no longer resolves when the post presses it (the flyout's content leaves the tree once written, and with it the `$parent` the binding walks to) runs the command it was written with, as Avalonia runs it in the open flyout.

## Library list

### ENGINE-240: Lists a library builds from its text are planned

- **Given** a library's templated control whose code builds, from its own text, a list its template shows (Aioros.Cosmo's `c:InputOtp`, whose `Slots` it fills from its `Text`, shown through `ItemsSource="{TemplateBinding Slots}"`), once with no binding, once disabled and once with its `Text` bound two way to the model's `Code`
- **When** the page is planned with an empty code
- **Then** the plan is reusable and writes what Avalonia renders, each slot written with its box (a property the control's code reads is learned, not a slot, ENGINE-219).

## Indexer binding

### ENGINE-241: Compiled integer indexers are planned

- **Given** compiled bindings through integer indexers of a list (`Border DataContext="{Binding Items[0]}"` around `{Binding Name}`, and `TextBlock Text="{Binding Items[1].Name}"`, in a view compiled with the app, which Avalonia describes as `Items.Item` with the index only in its generated getter)
- **When** the page is planned from one model and rendered for models with other items
- **Then** the plan is reusable, its slots read `Items[0]` and `Items[1].Name`, and it writes what Avalonia renders for every model, also read back from a plans file.

### ENGINE-242: Reflection integer indexers are planned

- **Given** the same bindings as reflection bindings (`{ReflectionBinding Items[1].Name}`, and XAML loaded at run time without compiled bindings, whose description keeps `Items[1].Name`)
- **When** the page is planned and rendered for models with other items
- **Then** the plan is reusable and writes what Avalonia renders for every model.

### ENGINE-243: Indexes out of range give no value

- **Given** the page of ENGINE-241 planned from a model of two items
- **When** it is rendered for a model of one item and for an empty list (the indexes out of range)
- **Then** the plan writes what Avalonia renders, a binding whose index is out of range giving no value as Avalonia's gives the property its default, and nothing throws.

### ENGINE-307: Precompiled indexers are described with their indexes

- **Given** a precompiled view (XAML compiled with the app, whose bindings describe an indexer by its property, `Source.Columns.Item`) with a check box bound to `Source.Columns[1].IsVisible` and a check box menu item bound to `Source.Columns[2].IsVisible`
- **When** its page is planned
- **Then** the fields' descriptions carry the indexes (`Source.Columns[1].IsVisible`, `Source.Columns[2].IsVisible`) as its rendering does, so a post through the plan unchecking the box hides the first column and pressing the menu item hides the second

## Data table

### ENGINE-244: Columns shown through an index are planned and toggled

- **Given** a `TableView` whose columns' `IsVisible` are bound through an index of the model's list of column options (`{Binding Columns[1].IsVisible}`, the shape of Aioros.Cosmo's data table's `Source.Columns[1].IsVisible`) and a `MenuFlyout` of check box items bound two way to the same flags
- **When** the page is planned
- **Then** it is reusable and the menu items' `aria-checked` and the columns' conditions read `Columns[n].IsVisible`
- **When** a column's menu item is posted
- **Then** its flag flips through the indexer and the answer leaves the column out.

## Calendar theme

### ENGINE-245: Calendars keep their month across posts

- **Given** a page with the themed calendar of RANGES-17 showing June 2025 and a button whose command counts
- **When** a post presses the calendar's next button
- **Then** the answer shows July 2025 and its hidden field names `2025-07-01|Month` (Avalonia moving to the first of the month)
- **When** a post carries that field and presses the counting button
- **Then** the command runs and the answer still shows July
- **When** a post carries it and presses the previous button
- **Then** June shows again.

*Precedent: ASP.NET Web Forms' `Calendar` (`VisibleDate` kept in the view state across posts, `VisibleMonthChanged` from its navigation links); the carousel of ITEMS-104 keeping its selection in a hidden field.*

### ENGINE-246: Clicked days are selected as on the desktop

- **Given** the themed calendar in `SingleDate` mode with `SelectedDate` bound two way
- **When** a post carries the field naming July 2025 and presses the day button that shows 18 July in July's month view
- **Then** the model's `SelectedDate` is 18 July 2025, Avalonia's own selection having run from a pointer press and release on that button; a post pressing a day of the next month selects it and shows that month, as on the desktop; a post pressing a blackout day leaves the selection as it was.

### ENGINE-247: Tapped ranges wait for their last day across posts

- **Given** the themed calendar in `SingleRange` mode with `AllowTapRangeSelection` and a library's attached `RangeStart` and `RangeEnd` bound two way (shaped as Aioros.Cosmo's `CalendarSelection`, following `SelectedDatesChanged`)
- **When** a post presses 9 June
- **Then** both are 9 June and the answer's field names 9 June as the range's waiting first day
- **When** the next post carries that field and presses 26 June
- **Then** the range is 9 to 26 June and the field names no waiting day, as two taps make the range on the desktop.

*Precedent: Avalonia's `AllowTapRangeSelection`; react-day-picker's `mode="range"`, where a first click sets `from` and a second `to`.*

### ENGINE-248: Control clicks add days

- **Given** the themed calendar in `MultipleRange` mode with a library's attached `Dates` bound two way to 3 and 12 June
- **When** a post presses 24 June with the field's modifiers naming Ctrl (`c`)
- **Then** the days are 3, 12 and 24 June, as a Ctrl click adds a day on the desktop
- **When** a post presses 13 June without modifiers
- **Then** the days are 13 June alone, as a plain click selects one.

### ENGINE-249: The header shows the year and a month shows its days

- **Given** the themed calendar showing June 2025
- **When** a post presses its header button
- **Then** the answer shows the year view of 2025 and its field names `Year`
- **When** the next post carries that field and presses the button of September
- **Then** the answer shows September 2025 in month mode.

### ENGINE-253: Literal dates give way to the month a post shows

- **Given** the themed calendar with `DisplayDate` and `SelectedDate` set as literals in the XAML (12 June 2025, as Aioros.Cosmo's `calendar-single` fixture)
- **When** a post presses its next button and the next post carries the answer's field and presses the button that shows 18 in July's month view
- **Then** the second answer still shows July, its field names July and 18 July is the selected day, the literals applying only to the page as it is built, before the post's field restores what the calendar shows.

### ENGINE-254: Presenter rooted buttons take their presenters look

- **Given** a themed calendar whose day and month buttons' templates are their content presenter (Semi.Avalonia's shape: the presenter takes the button's background, border, radius and padding, its theme sets their backgrounds under `:pointerover` and `:pressed` and a border under `:dayfocused` and `:btnfocused`)
- **When** it is rendered
- **Then** each day and month button is in the look of its presenter, its text its own, its pointed-at and pressed looks under the button's `:hover` and `:active`, the look of Avalonia's focused day (`:dayfocused`, `:btnfocused`) under `:focus-visible` and no browser outline, as Avalonia draws no focus adorner on a day but marks the calendar's current one; and Avalonia's `:focus-visible` on a day, which the calendar never gives it, is not written.

*Precedent: Avalonia's `CalendarItem`, whose calendar holds the keyboard focus and moves `IsCurrent` (`:dayfocused`) over its days; the WAI-ARIA date grid's focused day.*

### ENGINE-255: Star columns share the months width

- **Given** a themed calendar whose month view shares its width among seven star columns (`*`) under a 32 px row of day titles, inside a calendar item at least 240 px wide (Semi.Avalonia's shape)
- **When** it is rendered
- **Then** the month's table stretches to its box (`min-width:100%`), each column its share of it (`14.286%`), the titles' cells 32 px high; a month view of fixed columns keeps their pixels (RANGES-17).

*Precedent: Avalonia's `Grid`, which shares the width left among its star columns.*

### ENGINE-277: A calendar keeps its selection across posts

- **Given** a themed calendar (RANGES-17) whose selection is bound to nothing
- **When** a post selects a day
- **Then**
  - The answer's hidden field names the calendar's selected days after its display mode and modifiers (`2025-06-12|Month|||2025-06-03,2025-06-12~2025-06-13`, consecutive days as one run), and the next post restores them before its button is pressed, as ASP.NET Web Forms' `Calendar` keeps `SelectedDates` in its view state.
  - In `MultipleRange` mode without tap ranges (as Aioros.Cosmo sets it), Ctrl-clicks on 24 and then 13 June over 3 and 12 June leave 3, 12, 13 and 24 June selected, as the same clicks leave them on the desktop.
  - In `SingleDate` mode a day selected and the next and previous buttons pressed leave that day selected; a field without that part (an older answer) leaves the selection as the page's XAML and model give it.

## Culture

### ENGINE-250: Pages render in the culture of their caller

- **Given** a page with a themed calendar showing June 2025 and a `TextBlock` bound to a date with `StringFormat={}{0:D}`
- **When** the engine renders it from a caller whose current culture and UI culture are `fr-FR`
- **Then** the caption, the day titles and the date are French (`juin 2025`, `jeudi 12 juin 2025`); from an `en-US` caller, English; and a render that follows from an invariant caller is invariant, Avalonia's thread taking back its own culture after each job.

*Precedent: ASP.NET Core sets the request's culture on the thread that runs it; Avalonia formats with the current culture of its thread.*

### ENGINE-251: Plans are compiled in the culture of their caller

- **Given** the page of ENGINE-250
- **When** it is planned from an `fr-FR` caller
- **Then** the plan is reusable and writes what Avalonia writes in `fr-FR` (the calendar's French day titles, `jeudi 12 juin 2025`), rendered on a thread whose culture is `fr-FR`.

### ENGINE-252: Posts run in the culture of their caller

- **Given** a page whose button's command stores the current culture's name and formats a date in it
- **When** a post presses the button from an `fr-FR` caller
- **Then** the command runs under `fr-FR` and the answer shows the French date.

## Date picker theme

### ENGINE-256: Chosen days set the pickers date

- **Given** the themed picker of RANGES-24 with `SelectedDate` bound two way
- **When** a post presses the popover's calendar's day button that shows 18 June
- **Then** the model's date is 18 June 2025, set through the calendar's selection and the picker's (its `SelectedDate` bound two way to the picker, as Avalonia's), and the answer shows `June 18, 2025` with the popover closed, as Avalonia closes it when a day is released.

### ENGINE-257: Moving through months keeps the popover open

- **Given** the themed picker of RANGES-24
- **When** a post presses the popover's calendar's next button
- **Then** the answer shows July in the calendar with its popover open (`data-aioros-open`), as Avalonia keeps the popup open while the visitor moves through months, and the picker's date is unchanged.

### ENGINE-258: Typed dates are parsed in the current culture

- **Given** the `input` variant with `SelectedDate` bound two way and the short date format
- **When** a post carries `6/20/2025` in its field from an `en-US` caller
- **Then** the model's date is 20 June 2025
- **When** it carries `20/06/2025` from an `fr-FR` caller
- **Then** the same; and text that does not parse in the culture leaves the date as it was, as Avalonia's picker parses its text in the current culture when it loses the focus.

## State

### ENGINE-265: Control states are written in hidden inputs

- **Given** a library control whose theme sets `a:Web.State="Step,Mode,Note"` (an `int`, an enum and a `string` of its own, Aioros.Cosmo's questionnaire's place) holding a hidden library control that sets `a:Web.State="Picked"` (a `bool?`), and a page with no such control
- **When** the pages are rendered
- **Then** the first carries one hidden input per named property (`<input type="hidden" name="_s0" value="0">`, `_s1`, `_s2`, `_s3`) in the logical tree's order, each holding the property's invariant text, the hidden control's included, and the second carries none.

### ENGINE-266: Control states are restored before the button is looked for

- **Given** the control of ENGINE-265 at its first step, its `Next` button shown only at the first step and its `Finish` button only at the second
- **When** a post carries `_s0=1` and presses `Finish` (named as it is numbered on the second step)
- **Then** the properties are restored before the button is looked for, `Finish` is pressed (its click handler runs) and the answer shows the third step; a state value that does not parse (`_s1=Sideways`) or names no property any more leaves its property as it was, and the post still answers the page.

### ENGINE-283: A state bound to the page model is planned behind a layout

- **Given** a page behind a layout (its model in the layout's outlet) holding a library control whose `a:Web.State` names a property bound to the page's model (`Note="{Binding Title}"`)
- **When** the page is planned
- **Then**
  - The plan is reusable and renders the state's hidden input with the model's value (`Inbox`), read in the control's own data context as the outlet reads it, since the state inputs are written after the page's tree.
  - Found while closing Aioros.Cosmo's controlled and conditional questionnaires (`CurrentIndex` and a choice's `IsChecked` bound to the host's model) leaving their pages without a plan.

### ENGINE-284: A radio group written once per value keeps its field

- **Given** a text box and then a panel whose alignment is bound to the model, holding two radio buttons of one group
- **When** the page is planned (the panel written once per alignment) and rendered for each alignment
- **Then**
  - It writes what Avalonia renders: both radios name the group's field `f1` in every case, as a radio already numbered by an earlier write of the same panel gives itself its field again rather than pointing at an id that write never reaches.
  - Found while closing Aioros.Cosmo's questionnaire whose shortcuts are bound to the host (its radios written once per shortcut style) leaving its page without a plan.

### ENGINE-289: An attached state rebuilds the page before its post is bound

- **Given** a list holding one row field on its first page and two on its second, followed by a tail field, whose page is a library's attached property on the list it does not own (`m:Pager.Page`, registered for `ItemsControl` and bound two way to the model, as Aioros.Cosmo's `c:DataTable.State` on a `TableView`) named in `a:Web.State="Pager.Page"`
- **When** the page is rendered on its second page
- **Then** the attached property is written in its hidden input (`_s0` holding `1`)
- **When** a post carries `_s0=1` with the second page's row fields and the tail field
- **Then** the page is restored on its second page before the post is bound, so each value lands in its own field: the two rows and the tail.

*Precedent: ENGINE-265 and ENGINE-266 (a control's own properties), Avalonia's attached properties (`AvaloniaPropertyRegistry.GetRegisteredAttached`), ASP.NET Web Forms' view state (the page rebuilt before its post data is loaded).*

### ENGINE-298: A state bound two way is a state node of the plan

- **Given** library controls whose travelling state (`a:Web.State`) names a `string`, `int`, `double`, `bool` or `bool?` property bound two way to a model path
- **When** their page is planned
- **Then** the plan writes each state's hidden input `_s{n}` from the model through that path and records a state node at index `n` with the path and the property's type, in the model's scope (a page model behind a layout included); a travelling property set locally, bound one way or of an enum type writes its input as before and records no state node.

### ENGINE-300: Value inputs that write nothing are kept fields

- **Given** a text box, an auto-complete box and a combo box whose value binding writes nothing back (bound one way)
- **When** their page is planned
- **Then** each records a kept field (`kept`) instead of a field without a description; a text box bound two way under a data context bound to an item not chosen yet keeps its own description (the plan follows its path in that scope), a check box bound one way, whose absence from a post means unchecked, still records none, and value inputs not bound at all (a text box, a numeric up-down) record their own kind over the page's own state (ENGINE-310).

### ENGINE-304: A state value is sealed when a seal is in effect

- **Given** a library control's travelling state, bound or set locally
- **When** its page is planned and rendered
- **Then** its hidden input's value is a state value node: written as the value itself when no seal is in effect (the adapter's renders, which VerifiedPlans compares with Avalonia's), and as the seal of that value when one is (`StateSeals.Use`), in a document as in the plan's dynamics.

### ENGINE-305: A page compiled under a seal keeps its plan

- **Given** a page whose plan writes a library control's travelling state
- **When** it is compiled while a seal is in effect (a request's, which flows into the engine when a visitor reaches the page before its plan is compiled) and the seal writes the same value differently each time (as Data Protection's does)
- **Then** VerifiedPlans compares Avalonia's render and the plan's without the seal and keeps the plan: the page is reusable.

## Button theme

### ENGINE-267: Self bindings follow their propertys binding

- **Given** a library's button whose code binds its `a:Web.Copies` to its own `Value` (`RelativeSource Self`, Aioros.Cosmo's `c:CodeBlockCopyButton`) with `Value` bound to the model's `Code`, and a button whose `a:Web.Copies` is `{Binding $self.Tag}` with `Tag` bound to the same
- **When** the page is planned with one model and rendered for another, and a third library button whose `Value` is the literal `npm i`
- **Then**
  - The plan is reusable, both bound buttons copy each model's `Code` and the third `npm i`, as Avalonia renders them: a binding to another property of its target itself (`$self.Property`, `RelativeSource Self`) follows that property's binding, as `#name.Property` does (ENGINE-34), and writes its value when that property is not bound.
  - Found while closing Aioros.Cosmo's need 70: the copy button's bound `Value` left the page without a plan.

### ENGINE-269: A state look follows the controls place

- **Given** four buttons wearing one control theme whose `:focus-visible` look takes its border from a dynamic resource, with the same values at rest, where styles in scope give another focus ring to a button inside a `Border` with the `invalid` class (`Border.invalid > Button`) and to one inside a horizontal `StackPanel` (`StackPanel[Orientation=Horizontal] > Button`), and the fourth button's `Border` overrides the resource
- **When** the page is rendered in each theme, the plain button first
- **Then** each button's state rules are the ones Avalonia gives that button in its place: the plain ring and border, the `invalid` ring, the horizontal ring and the overriding border; a look read for one control (a theme's state look, a mark's look, a tip's look) is never reused for a control whose ancestors differ in what styles and resources can read (their types, names, classes, the properties the styles in scope match, their styles and resources), on the same page or a later one in the same process.

*Precedent: Avalonia matches a style's selector against the control's ancestors (child and descendant combinators, property matches) and resolves a dynamic resource up its ancestors, so a look depends on the control's place as well as on its own values.*

### ENGINE-274: A look follows what the control fixes itself

- **Given** one engine that renders, in this order, a page whose `Button` has its own `Template` with no content presenter (a panel of parts), a page whose `Button` has a local `RenderTransform`, a page whose `Button` has an explicit `Theme` defined in the page, and then a page whose plain Fluent `Button` stands in the same place (the same ancestors, classes and sizes)
- **When** each page is written
- **Then** the plain button carries Fluent's look as a fresh engine writes it (its 1 px transparent border and its pressed `scale(0.98)`), and each earlier button keeps its own (no border look for the panel of parts, no pressed scale under a local transform, its theme's look for the themed one), whichever order the pages come in: a cache of looks is keyed on what the control fixes itself outside styles (its template's realized parts and the values the template sets, its local values, its explicit theme described by content, never by object identity), as it is on its place (ENGINE-269).

*Precedent: Avalonia's value priorities (a local value and a template's value win over a style's, so they decide a look as much as the selectors do), and keys by content as React's and Vue's compiled templates hash their static parts.*

## Toggle theme

### ENGINE-270: A mark look is read for the controls own size

- **Given** two check boxes of one theme side by side in one panel, the first with a one-line label and the second 120 px wide with a label that wraps to three lines
- **When** the page is rendered
- **Then** the second check box's element carries the height Avalonia lays it out at (its mark look read for its own size, not the first one's), the same class a page holding only the second check box writes for it; and a check box whose label holds a line break is read for its own two lines.

*Precedent: Avalonia measures a control with its own `Width`, `MinHeight`, `MaxHeight` and content, so two controls of one theme and classes lay out at different heights.*

### ENGINE-275: A marks rest is the unchecked box

- **Given** one engine that writes first a page whose two-state `CheckBox` holds a null `IsChecked` (Avalonia draws its indeterminate glyph, as a table's select-all box does with some rows selected), then a page whose same `CheckBox` in the same place is unchecked
- **When** both are written
- **Then** both carry the same mark class, whose resting look is the unchecked box (no glyph) with the checked look under its checked state, as a fresh engine writes for the unchecked box: a mark's resting look is read with the control unchecked and not indeterminate, so the value it held when it was first written never becomes every box's rest.

*Precedent: the `:unchecked` rest the mark looks already read (BUTTONS-113), Avalonia's `:indeterminate` pseudo-class.*

## Media controls

### ENGINE-276: An icon draws the markup registered last

- **Given** an icon kind the `Icon` control reads before any icon set registers it, then registered with one stroke, then registered again with two
- **When** the control draws it each time
- **Then** it draws nothing, then the one stroke, then the two: the strokes are parsed once per markup, never kept for a kind whose markup the icon set has since changed.

*Precedent: the icon set's own rule that a later registration of a kind wins (`IconSet.Register`), and the looks' caches keyed by content (ENGINE-274).*

## Calendar plan

### ENGINE-278: Bound calendars the plan cannot follow render literally

- **Given** a bound themed calendar whose library gives each even day of the month a bold text (a look the day's state does not name), and one with a `DisplayDateStart`
- **When** each page is compiled
- **Then** its plan is not reusable, its failure naming the reason (`a bound Calendar whose days' looks depend on more than their state…`, `a bound Calendar with blackout days or a display range`), and the page renders as Avalonia does.

## Motion

### ENGINE-279: A page the engine writes is marked written

- **Given** a page whose `TextBlock` shows its own `a:Web.IsWritten` (`{Binding $self.(a:Web.IsWritten)}`)
- **When** the engine writes the page
- **Then** the text is `True`: the window the engine realizes a page in carries `Web.IsWritten` and every control of the page inherits it, so a library can skip what only a screen needs (an exit that keeps a closed overlay in the tree); nothing else sets it, so a window the native targets show reads `False`.

*Precedent: Avalonia's `Design.IsDesignMode`, which tells a control it is realized in a designer rather than shown.*

## Engine priority

### ENGINE-280: A request runs before queued background work

- **Given** jobs queued on the engine's Avalonia thread while it is busy, three of them background work (the startup warm-up that compiles every page in each culture and theme, run under `BackgroundWork`) and one a request's, queued after them
- **When** the thread is free
- **Then** the request's job runs before the queued background jobs, so a visitor waits at most for the background job already running instead of the whole warm-up (Aioros.Cosmo's Gallery tests timed out on a 17 s request behind it, 3.3 s on its own); and the background jobs still all run, in their order.

*Precedent: Avalonia's `DispatcherPriority` (`Background` below `Default`), WPF's dispatcher priorities, ASP.NET Core's `BackgroundService` guidance that background work must not starve requests.*

## System face

### ENGINE-281: A weight a family lacks gets its real nearest face whatever was asked first

- **Given** a system font family with only some weights (Liberation Sans: regular and bold, upright and italic) set on a page
- **When** the engine compiles the page for the web (which asks Avalonia for its regular face first) and a text in that family then asks for the semibold weight
- **Then** the text gets the family's real bold face with no synthetic emboldening, the face CSS's font matching gives a weight of 600 when only 400 and 700 exist, as wide as a bold text, as it does in a process that never asked for the regular face first: every face of a system family the application or a composed page names is loaded before it is used, since Avalonia 12.1.3's system font collection resolves a weight a family lacks to the nearest face it has already cached.

*Precedent: CSS Fonts 4's font matching (the nearest real face, a weight above 500 looking heavier first); Avalonia's `SystemFontCollection.TryGetGlyphTypeface`, whose nearest match runs over the faces cached so far.*

## Modal

### ENGINE-287: Dialogs open from the start are planned with their entrance

- **Given** a library dialog open from the start whose mask and surface fade in as they are shown (`MarkedModal` with `Moving` and `OpensAttached`, shaped as Aioros.Cosmo's `c:Dialog` bound open)
- **When** the page is planned
- **Then** the plan is reusable and writes what Avalonia renders, the surface with its leaving and entering classes: both compiles write the entrances the page was in the middle of, settled once before the first.

### ENGINE-299: A bound dialogs open state is a state node of the plan

- **Given** a library's overlay marked `a:Web.Modal` whose open property is bound two way to a model path
- **When** its page is planned
- **Then** the open state's hidden input `_m{n}` (ENGINE-303), written in its dialog, records a state node at its index with that path, so a post carrying it (Aioros.Cosmo's dashboard-01 posts its row viewer's open state with every post) is answered by the plan with the bytes the engine writes; an overlay whose open property is not bound records none.

### ENGINE-303: An overlays open state is its own field

- **Given** a library's overlay (`a:Web.Modal`) beside a library control with a travelling state
- **When** their page is rendered and posted
- **Then** the overlay's open state is written and restored as `_m{n}` (its dialog's `data-aioros-modal`, its hidden input, the plan's state node) and the control's as `_s{n}`, one numbering for both, so a post opens the overlay from `_m{n}` as it did from `_s{n}`: the browser changes an overlay's open state itself and only a library's travelling state is sealed (decision "Sealed control state").

## Deferred engine

### ENGINE-290: Pages whose models use Avalonia start the engine before their models

- **Given** a production app's engine that starts on demand and has not started
- **When** a page whose definition carries `ModelsUseAvalonia` is loaded
- **Then** the engine starts (Avalonia's platform, with its UI dispatcher on the engine's thread) before the page's models are created, and they are created where the page is rendered, as for any page: on the request's thread when its plan renders it, on the engine's thread when the engine does; when a page without it is loaded from its plan, its models are created on the request's thread and the engine does not start.

## Taken dispatcher

### ENGINE-291: An engine whose dispatcher another thread took refuses to start

- **Given** Avalonia's UI dispatcher already taken by another thread (an Avalonia object created there before the engine started)
- **When** the engine starts
- **Then** it fails at once with an `InvalidOperationException` that names that thread and says that a page's model created an Avalonia object outside Avalonia's thread before the engine started, instead of Avalonia's "The calling thread cannot access this object because a different thread owns it" from inside its platform's setup.

## Engine nesting

### ENGINE-296: Queued engine jobs never run inside a jobs pump

- **Given** an engine job on Avalonia's thread that settles its page by pumping Avalonia's dispatcher from inside its run (as layout, probes and posts do), while two other engine jobs are queued behind it (a request's and a background one, the startup warm-up's compile)
- **When** it pumps
- **Then**
  - The work Avalonia posted during the job runs within that pump, and neither queued engine job starts until the running job has returned; they then run one at a time, the request's first.
  - Found measuring Aioros.Cosmo's dashboard-01 during the Gallery's warm-up (2026-10-27): a settle inside a post ran whole warm-up compiles nested in it, and those ran further jobs in their own settles, so posts waited 15 to 45 s.

*Precedent: React's scheduler, which keeps its own task queue apart from the browser's event loop and runs one task at a time.*

### ENGINE-297: Queued engine jobs never run inside a continuations pump

- **Given** an asynchronous engine job whose continuation, after an `await`, pumps Avalonia's dispatcher while another engine job is queued
- **When** the continuation pumps
- **Then** Avalonia's posted work runs within the pump and the queued engine job starts only once that continuation has returned to the dispatcher; while the asynchronous job waits between its continuations (a command awaiting its prompt), other engine jobs run.

### ENGINE-302: A full engine queue refuses the next request

- **Given** 256 requests' jobs waiting on the engine's thread (queued while a job runs)
- **When** one more request's job is queued
- **Then** it is refused at once with `EngineBusyException` and the 256 still run, while background work (the warm-up) is never refused: a flood of requests that need the engine would otherwise grow memory and make every such request wait without bound (the security audit's F3; precedent: ASP.NET Core's `ConcurrencyLimiter` queue limit).

## Failure position

### ENGINE-306: A plan failure names its place in the XAML

- **Given** a page whose XAML holds an element that keeps its plan from being recorded
- **When** the page is compiled
- **Then**
  - The compiled page's `FailureAt` names that element's place in the app's XAML: the page's source (`Pages/keys/Page.axaml`), the line and the column Avalonia's XAML compiler records (`XamlSourceInfo`): the control being written when a writer fails the plan (a `KeyBinding` without a command, on its panel), or the control whose binding no slot reads when the plan is checked after writing (a bound `Tag`); a position inside a library's XAML (Avalonia's themes) is passed over for the nearest of the app's; a page whose plan is recorded has none.
  - XAML loaded at run time (hot reload, the engine's tests) records positions too.

*Precedent: TypeScript's and Roslyn's diagnostics, located at the span that caused them; Svelte's and Vue's template compilers, whose errors point at the template line.*

## Local field

### ENGINE-310: Inputs not bound are planned over the pages own state

- **Given** a page with value inputs not bound at all and not named (a check box, a toggle switch, a toggle button, a slider, a text box, a numeric up-down, a combo box and a list box of strings, outside any repeated view)
- **When** it is planned (decision "Systematic exploration of the trace")
- **Then** each records its own field kind over the page's own state (a local path, `@k`, holding its value's type and the value the trace saw) instead of no description; the page renders as Avalonia does with no post; and a post changing every one of them, answered by the plan alone (`PlanForms.ApplyAsync` within `LocalValues.Use`), leaves the models as they were and renders the page byte for byte as the engine renders it after the same post.

*Precedent: React's uncontrolled inputs, whose value is the input's own state, and Phoenix LiveView, where the client holds the value of an input the server does not own.*

### ENGINE-313: Only inputs written once read local paths

- **Given** a page whose root has no data context of its own under a layout's model, with a local toggle switch, and a list whose items, generated from strings, each hold a check box
- **When** it is planned
- **Then** the toggle switch reads a local path and the items' check boxes do not (their controls repeat for each item), while a list's own items written in the XAML (a list box item holding a check box) are written once and read local paths.

### ENGINE-315: Inputs only the visual tree holds read local paths

- **Given** a drawer page whose drawer header and drawer hold local inputs (a toggle switch, a check box), which the drawer page shows without making them its logical children, beside a local text box in its content page
- **When** it is planned
- **Then** each of the three reads a local path (the page's inputs are looked for in its logical tree, then in what only its visual tree holds that no template built); a template's own parts (a numeric up-down's text box) are not looked for.

### ENGINE-316: Named inputs and the bindings that read them share their local paths

- **Given** a page whose named local inputs other elements read (a toggle switch `more` whose `IsChecked` a text's `IsVisible` binds as `#more.IsChecked`, and a slider `level` that snaps to its ticks every 10, whose `Value` a progress bar binds as `#level.Value`)
- **When** it is planned
- **Then** the inputs and the bindings that read them share their local paths, so the page renders as Avalonia does with no post, and a post switching the toggle on and giving the slider 37, answered by the plan alone, renders byte for byte as the engine does after the same post (the text shown, the slider and the bar at 37: Avalonia snaps a slider to its ticks as it is dragged, not as its value is set, and a browser posts only its steps).

### ENGINE-317: Toggle split buttons and pips pagers are planned over the pages own state

- **Given** a page with a toggle split button and a pips pager of five pages, neither bound
- **When** it is planned
- **Then** each records its field over the page's own state (`toggle` and `page` over local paths) and the pager's previous and next buttons step its page (`page-step`, within its pages, as Avalonia's pager steps it), and a post switching the toggle and pressing next, answered by the plan alone, renders byte for byte as the engine does after the same post.

### ENGINE-318: A librarys selecting control is not the local state of the page

- **Given** a page with a library's selecting control that is not bound (a list of the test library's own type, whose selection the library may act, as Ursa's navigation menu navigates) beside an Avalonia list box, neither bound
- **When** it is planned
- **Then** the list box reads a local path and the library's control does not, so the plan never holds its value.

### ENGINE-320: Only inputs under a guaranteed theme hold the pages own state

- **Given** a page of inputs not bound (decision "Guaranteed scope, closed per control type", verified per page): a check box, a toggle switch and a text box under the Fluent theme, a toggle button whose control theme is the page's own (a `ControlTheme` for `ToggleButton` in the page's resources), a check box a style of the page moves when checked (`CheckBox:checked`) and one a style of the page bolds by its value (`CheckBox[IsChecked=True]`, a property selector)
- **When** it is planned
- **Then** all six read local paths, the first three under the guarantee and the other three marked outside it (`@k!~type`), so their posts are answered by the plan only once the page's verification has walked their domains (SSR-311, SSR-312): an input is guaranteed only under a guaranteed theme (Fluent, Aioros.Cosmo) with no style outside it that changes with its state, by a pseudo-class or by a property; nothing is set on any control while the page is compiled.

### ENGINE-321: Only inputs no code outside the guarantee observes hold the pages own state

- **Given** inputs not bound under the Fluent theme
- **When** their page is planned
- **Then** an input reads a local path only when no code outside Avalonia and the guarantee could observe it: a check box inside a panel of the app's own type that declares nothing but its constructor reads one; a check box inside a panel of the app's own type that declares a handler, one carrying an attached property of a library (the test library's), and a slider named `ratio` whose value a library control reads (`{Binding #ratio.Value}` on the test library's proportion decorator) record no description.

### ENGINE-325: An input another element writes back is not the pages own state

- **Given** a toggle button `toggle` whose `IsChecked` a split view binds two way (`IsPaneOpen="{Binding #toggle.IsChecked, Mode=TwoWay}"`) beside a check box no binding writes back
- **When** the page is planned
- **Then** the check box reads a local path and the toggle button records no description, its posts left to the engine: an input another element's binding writes back is not the page's own state, since that element changes it (the split view closing its pane), and the plan never takes it for a constant either.

### ENGINE-332: A list of equal items is not the pages own state

- **Given** a combo box whose items are equal two by two (`Two` and `Two`) beside one whose items differ
- **When** the page is planned
- **Then** the first records no description, its posts left to the engine, and the second reads a local path: Avalonia resolves a choice among equal items to the first of them (choosing the second shows the first chosen), which the page's own state, an index, would not

## Local domain

### ENGINE-322: Each local input type renders every value of its domain as Avalonia does

- **Given** each input type the plan holds the page's own state of (a check box, a three-state check box, a toggle switch with on and off contents of different widths, a toggle button, a toggle button showing an icon (whose color it inherits), a toggle split button, a list box and a combo box of three strings, a combo box with a placeholder and items of different widths whose content is aligned left (as Cosmo's select aligns it), a combo box with no width of its own, a tab control of three tabs, a slider, a text box, an auto-complete box, a numeric up-down, a calendar date picker, a date picker, a time picker and a pips pager) under the Fluent theme
- **When** each sampled value of its domain is set (each flag, each index and none, the two ends of a range and its middle, an empty, a short and a long text, two dates and two times)
- **Then** the plan's render of that value from the page's own state is byte for byte Avalonia's render of the control holding it: the type is closed over its domain.

### ENGINE-323: The domain walk reports what it cannot close

- **Given** the domain walk of `Aioros.Testing` (`LocalDomains.DisagreementsAsync`), which a library's or an app's tests call for each input type they ship
- **When** it walks a check box whose content is bound to a model that renders differently on every realization, a page that holds no local input (a bound text block) and a page whose plan is refused (an unbound key binding's command)
- **Then** it reports, in that order, each value of the check box's domain (`check=on`, `check=(none)`) with the position where the two renders part, `holds no local input`, and `not reusable:` with the plan's failure; it never reports a page closed over its domain when it could not walk it.

### ENGINE-324: Inputs that follow one another render every value and pair as Avalonia does

- **Given**
  - Pages whose local inputs other controls follow through bindings to them under the Fluent theme (a check box enabling a check box, `IsEnabled="{Binding #a.IsChecked}"`; a switch making a text box read-only; a check box showing a toggle button; a text box echoed by a text block; a slider shown by a progress bar; three independent check boxes; a check box enabling a combo box; a check box disabling a slider, `{Binding !#a.IsChecked}`; a chain of three check boxes, each enabling the next; a check box hiding a list box written before a slider; a check box disabling another, `IsEnabled="{Binding !#a.IsChecked}"`, both changed in one post; a switch enabling a second switch and showing a slider written after it, both switches turned on in one post)
- **When** `LocalDomains` posts each value of each input's domain and each pair of values of each two inputs
- **Then**
  - It reports nothing.
  - The plan follows a binding to a local input's value through its local path (a binding to an element is a constant of the page only when that element's value cannot change).
  - Describes the page's fields as the post leaves them while it writes the same fields (the page's local inputs are bound one at a time in Avalonia's order, the page described again after each that changes, so an input another enables or disables is bound as it then is, its posted value dropped while disabled; an input another hides keeps the names the browser posted for the fields after it).
  - Renders every value and pair as Avalonia does.
  - A text box whose watermark reads a check box (`Watermark="{Binding #a.IsChecked}"`) is not planned, its binding not followed, rather than written with the value the trace saw.

### ENGINE-326: A state style on the control itself renders every value as Avalonia does

- **Given** inputs a style of their page changes in a state on the control itself (`CheckBox:checked`, on a two-state and a three-state check box, `ToggleSwitch:checked` and `ToggleButton:checked` setting `Foreground`, `FontWeight`, `FontSize`, `Opacity`, `Margin` or `Padding`, the content inheriting the color)
- **When** `LocalDomains` walks their domains
- **Then**
  - It reports nothing: the control's own style is written as it is at rest and each state's change as a rule of that state (`.mk…:has(>input:checked){color:…}`), resolved by Avalonia's style system as the states' looks are, so the plan and Avalonia write the same bytes for every value instead of the style of the value the trace saw.
  - A check box's `Padding` in a state, which moves its content from its mark, is not among them: its page's verification leaves it to the engine.

### ENGINE-330: A style selecting an input by its value renders every value as Avalonia does

- **Given** inputs a style of their page selects by their value rather than by a state (`CheckBox[IsChecked=True]`, `ToggleSwitch[IsChecked=True]`, `ToggleButton[IsChecked=True]` setting `FontWeight` or `Foreground`, and `CheckBox[IsChecked=False]` setting `Opacity`)
- **When** `LocalDomains` walks their domains
- **Then** it reports nothing: while their looks are read in each state, a toggle holds the value of the state (`True` checked, `False` at rest, none indeterminate), so a property selector applies where `:checked` would and the state's rule carries its look; the input stays outside the guarantee (ENGINE-320), proven by its page's verification.

## Generated page

### ENGINE-327: No generated page under the guarantee disagrees with Avalonia

- **Given** pages generated from a grammar of the XAML Aioros writes (Avalonia's inputs under the Fluent theme, with classes, margins, bindings from one input's value to another's `IsEnabled` or `IsVisible`, negated or not, text blocks reading an input, styles of the page whose selectors name a type or a class with a state pseudo-class or a property and set its foreground, background, font, opacity, margin, border or padding, and control themes of the page), each from its own seed
- **When** each is walked by `LocalDomains` (every value of every local input, and every pair)
- **Then**
  - No page whose inputs are all under the guarantee disagrees with Avalonia.
  - Each page is counted as closed, refused (its plan not reusable, so the engine answers it), without local inputs, unwalked (an input whose domain holds one value), declined (the plan answers none of a value's posts, so the engine answers them).
  - Or outside the guarantee and disagreeing, which the run reports with its seed and its reduced page (ENGINE-328), a reduced page all of whose inputs are under the guarantee counting as a disagreement under it.
  - The run walks the seeds `FUZZ_SEED` onwards, `FUZZ_COUNT` of them (40 by default), and writes its report where `FUZZ_OUT` names, with its bugs beside it as JSON (`.json`, one entry per smallest failing page with its seeds and failure) for the nightly run to file as issues.

### ENGINE-328: A failing page is reduced to the smallest page that still fails

- **Given** a page that disagrees with Avalonia beside controls and styles that play no part in it (a text box a style bolds by its text, `TextBox[Text=x]`, among a check box, a slider, a toggle button and two styles of other types)
- **When** it is reduced
- **Then** the reduced page still disagrees and holds only the text box and its style: the reduction removes one element, attribute or style at a time while the page still disagrees, so each gap the generator finds is reported as the smallest page that shows it.

### ENGINE-331: No valid page from avalonias metadata under the guarantee disagrees with Avalonia

- **Given** pages generated from Avalonia's own metadata rather than from a grammar written by hand (any public control of `Avalonia.Controls` with a public constructor, nested as its content model allows: a panel's children, a content control's content, a decorator's child, a list's items, a text's text; each control given a few of its settable styled and direct properties, each with a value of its type: a flag, a size, a count, a text, an enum's member, a thickness, a corner radius, a brush), each from its own seed
- **When** each is loaded by Avalonia and, when Avalonia renders it, walked by `LocalDomains`
- **Then** a page Avalonia rejects (it fails to load or to render) is counted as invalid and judged no further; no valid page whose inputs are all under the guarantee disagrees with Avalonia; and the run reports the share of valid pages closed, refused with the reasons the plan gave (each reason counted, so the map of what the engine answers comes out of the run), without local inputs, or outside the guarantee and disagreeing, each reduced (ENGINE-328); the run walks the seeds `META_SEED` onwards, `META_COUNT` of them (30 by default), and writes its report where `META_OUT` names, with its bugs and crashes beside it as JSON (`.json`) as ENGINE-327's.

## Keyed list posts

### ENGINE-400: Items name their fields and buttons by key

- **Given** a bound `ItemsControl` of items with an `Id`, each holding a two-way `TextBox` and a button with a command
- **When** the page is rendered by the engine and by its plan (also read back from a plans file)
- **Then** both write the same page: each field's name and each button's value carry the item's key after the field's token (`f1~` and `0~` followed by 24 hexadecimal digits of a hash of the key's text, ENGINE-430), the same item giving the same key whatever its position, and a field outside the list carries none.

*Precedent: React's `key`; LiveView's stream DOM ids; Aioros's own repeated-view tokens (`{slot}-{item}.{id}`, GENERATOR-46).*

### ENGINE-401: An items identity comes from the list its ID or itself

- **Given** bound lists of items whose identity comes from `a:Web.ItemKey="Code"` on the list (over an `Id` the item also has), from an `Id` member, from the item itself (a string), and of items with none of these
- **When** the pages are rendered
- **Then**
  - The first's key follows its `Code`, the second's its `Id`, the third's the string, and the fourth's fields carry no key, stay numbered by position and bind by position as before.
  - A list of a template selector or of several typed templates takes its identity only from `a:Web.ItemKey`, since its items need not share a member.

### ENGINE-402: A post of an item no longer there binds and runs nothing

- **Given** a page written for the items `a`, `b`, `c`
- **When** a post carries a field and the button of `b` and the fresh list no longer holds `b`, or holds `a` no more so that `b` stands at another position
- **Then** the plan and the engine answer `PostStatus.Stale`, no field of any item is bound and no command runs.

### ENGINE-403: A reordered list is stale and the same list binds its own item

- **Given** a page written for the items `a`, `b`
- **When** a post carries a field and the button of `b` and the fresh list is `b`, `a` (reordered) or `x`, `a`, `b` (an item inserted before)
- **Then** the plan and the engine answer `PostStatus.Stale`, nothing is bound and no command runs
- **When** the fresh list is the page's
- **Then** `b` alone takes the typed text and its command runs, on both paths.

### ENGINE-404: A list that follows a posted field is looked at once that field is bound

- **Given** a page whose list depends on a field of the page outside the list (a filter text box posted with the rows it filtered)
- **When** a post from the filtered page carries the filter, a row's field and that row's button
- **Then** both paths bind the fields outside the list first and look at the list again before they bind its items: the row the browser showed takes the value and its command runs, with no `Stale`; when the list after that is not the page's either (the row is gone), the answer is `PostStatus.Stale`.

### ENGINE-405: The field a post names as changed is not bound before the list is looked at

- **Given** a page whose list depends on a field the post names as changed (`_chg`, a filter whose new value is not the one the page was written for)
- **When** the post carries the rows the page showed
- **Then** the changed field is left out of the fields bound before the list is looked at: the rows the browser showed take their values on both paths, with no `Stale`, and the changed field is bound after them.

### ENGINE-406: A bare token binds by its position

- **Given** a keyed list
- **When** a post names a field or a button by its bare token, without a key (a page written before the keys, a post by hand)
- **Then** both paths bind it by its position as before and the post is not stale; a token whose key is not the item's at that token is stale (ENGINE-402).

### ENGINE-407: An items travelling state names its item

- **Given** a keyed list whose items carry a travelling state (`a:Web.State="IsExpanded"`)
- **When** the page is rendered
- **Then** each state's hidden input carries its item's key (`_s0~1a2b3c4d`, numbered within the item) and a post restores it into that item
- **When** the fresh list no longer holds the item at that state's position
- **Then** the answer is `PostStatus.Stale` and no state is applied to another item.

### ENGINE-408: Fields in a list boxs items name their item

- **Given** a `ListBox` bound to items with an `Id` whose template holds a text box and a button
- **When** the page is rendered by the engine and its plan
- **Then** both write the same page: the selection's own field stays `f0`, the item's field and button carry the item's key, and a post naming an item no longer there answers `PostStatus.Stale`, nothing bound and no command run, while the same list binds that item alone.

### ENGINE-409: Fields in a trees items name their item

- **Given** a `TreeView` bound to items with an `Id` and children, whose template holds a text box
- **When** the page is rendered by the engine and its plan
- **Then** every field carries a key made of its item's and its ancestors' (three items, three keys); a post naming a child no longer there answers `PostStatus.Stale` on both paths, and the same tree binds that child.

### ENGINE-410: A list whose shape changed refuses a post that names none of its items

- **Given** a region (the page, or a view's) that wrote a bound list and a field
- **When** it is rendered
- **Then** it ends with `<input type="hidden" name="_shape" value="{8 hex}.{first}">`: a digest of the items of its lists that write fields and the id of the first field written in a list
- **When** a post carries that mark and a field below the list, and the fresh list is shorter
- **Then** both paths answer `PostStatus.Stale` and nothing is bound, though the post names no item (rows with only unchecked boxes); the same list binds the field.

### ENGINE-411: A list that was empty on the page is a changed shape

- **Given** a page whose bound list was empty when written (its mark has no first field)
- **When** a post carries the mark and the list now has items
- **Then** both paths answer `PostStatus.Stale`, a field posted under a name an item now holds not being bound to it; when the list is still empty, the post binds as written.

### ENGINE-412: A list without identity is counted and warned of

- **Given** a bound list with posted controls whose items have no identity
- **When** the page is planned
- **Then**
  - The compiled page carries the warning `AIOROS033` naming `a:Web.ItemKey`, its names stay bare, and its mark counts the items: a post from a page with another count answers `PostStatus.Stale` on both paths.
  - A list of the same count whose items were replaced or reordered is not detected, since nothing identifies them, and binds by position: that is what the warning tells the author to fix.

### ENGINE-413: A keyed items state is written and restored from the plan

- **Given** a keyed list whose items carry a travelling state bound two way
- **When** the page is planned
- **Then** the plan stays reusable and writes each state in its item, numbered within it and named with the item's key (`_s0~1a2b3c4d`), as the engine does; a post restores it into that item through the plan and the engine, and a state of an item no longer there answers `PostStatus.Stale`.

### ENGINE-414: A refused post leaves the models as they were

- **Given** a post that is refused for its lists after the fields before them or its travelling states were bound to look at them (the filter of ENGINE-404 with the row gone, a state with a mark that differs)
- **When** both paths answer `PostStatus.Stale`
- **Then** the models hold the values they held before the post: a refused post leaves nothing on a model the app shares.

### ENGINE-415: A changed field before a list that follows it binds alone

- **Given** a page whose list follows a field before it
- **When** a post names that field as changed (`_chg`) and the list cannot be matched (the browser showed it for an earlier value the post no longer carries)
- **Then** both paths bind the changed field alone, no item's field, and answer the page; a changed field below a list whose shape changed answers `PostStatus.Stale`, since its name may stand for another control.

### ENGINE-416: A menu in a keyed item names its item

- **Given** a keyed list whose items hold a button with a menu flyout
- **When** the page is rendered by the engine and its plan
- **Then** the menu items' buttons carry the item's key, and a post pressing one of an item no longer there answers `PostStatus.Stale`.

### ENGINE-430: IDs chosen to collide are still different items

- **Given** a keyed list whose page wrote the item `40189`, and a list where the item `797186` now stands in its place (two ids whose 32-bit FNV-1a hashes are equal, as a person who chooses ids can construct offline)
- **When** a post from that page names its field and button
- **Then**
  - The post is refused as stale on the plan and on the engine and the other item takes nothing.
  - An item's key is 96 bits of SHA-256 (24 hexadecimal digits), so two ids cannot be made to collide.

*Precedent: Git's move from SHA-1 after a constructed collision; content-addressed names truncating a cryptographic hash.*

## Container query

### ENGINE-417: A list traced in a container query counts its items once

- **Given** a bound list whose items hold a field, inside a container with a `ContainerQuery` (so the writer traces the container in each state of its query before it writes it)
- **When** the page is compiled
- **Then** the plan is reusable: the region's shape mark counts each item once on the engine as on the plan, a trace dropping the shape it added along with its fields.

### ENGINE-418: A bound list in an unbound list counts in its region

- **Given** a bound list whose items hold a field, placed as a child of an `ItemsControl` with no `ItemsSource` of its own (Aioros.Cosmo's `c:CommandGroup` in a `c:Command`)
- **When** the page is compiled
- **Then** the plan is reusable and the list counts in its region's shape on the engine as on the plan: only a list inside an item of another bound list is a hierarchy, which the plan writes as one tree.
