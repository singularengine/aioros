# Notice

`Stylet.Avalonia/` and the four samples in `StyletSamples/` are ported from
[sealoyal2018/Stylet.Avalonia](https://github.com/sealoyal2018/Stylet.Avalonia) (commit `f56678d`, version 0.5.1),
Copyright (c) 2023 sealoyal, under the MIT license in `LICENSE-Stylet.Avalonia.txt`. Stylet.Avalonia is a port of
[canton7/Stylet](https://github.com/canton7/Stylet) to Avalonia.

| Here | Source |
| --- | --- |
| `Stylet.Avalonia/` | `Stylet.Avalonia/` |
| `StyletSamples/Hello/` | `Samples/Stylet.Samples.Hello/` (`MainView`, `MainViewModel`) |
| `StyletSamples/HelloDialog/` | `Samples/Stylet.Samples.HelloDialog/` (`ShellView`, `ShellViewModel`, `Dialog1View`, `Dialog1ViewModel`) |
| `StyletSamples/NavigationController/`, `StyletSamples/Pages/navigation/Layout.axaml` | `Samples/Stylet.Samples.NavigationController/` (`Pages/ShellView.axaml` became the layout) |
| `StyletSamples/TabNavigation/` | `Samples/Stylet.Samples.TabNavigation/` |

Changes, in three steps:

1. Upgrade the library to .NET 10 and Avalonia 12: the project takes the repository's package versions,
   `[ConstructorArgument]` (gone from Avalonia 12) is dropped from `ActionExtension`, its XAML keeps reflection
   bindings (`AvaloniaUseCompiledBindingsByDefault` false, Avalonia 12's default is true), a duplicate global using is
   removed, and nullable warnings are off (`Nullable` set to `annotations`) as they were not errors upstream.
2. Stylet fixes and hosting: `BindableCollection.OnCollectionChanging` raises `CollectionChanging` only when it has
   handlers again (upstream commit `f56678d` dropped the check, so every `Add` without a handler threw), and
   `IoC.GetInstance`/`GetInstances` are public so a host other than `StyletApplication` can supply them.
3. Aioros: windows became user controls placed by pages (`Pages/`), each view names its view model (`x:DataType`)
   and its action target (`s:View.ActionTarget="{Binding}"`, which Stylet's `ViewManager` set), `s:View.Model`
   became a bound `Content` with explicit `DataTemplate`s, Hello's `Click` action is a `Command` action, and the
   composition (`Program.cs`, `Composition/`) replaces `StyletApplication`, StyletIoC and the view lookup by name:
   services registered in Microsoft's container, a view manager that maps the two window view models to their
   windows, the navigation controller mapped to routes and a lifecycle that activates the page's screens.
