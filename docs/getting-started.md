# Getting started

This guide takes you from nothing to a page running in the browser and in a native window. It takes a few minutes, and the only thing you need beforehand is the [.NET 10 SDK](https://dotnet.microsoft.com/download).

> [!NOTE]
> Aioros is in public preview. Its APIs can change between versions, and it is not ready for production yet.

## Create an app

Install the template and create an app:

```bash
dotnet new install Aioros.Templates@0.1.0-preview.1
dotnet new aioros -n MyApp
cd MyApp
```

An app is a folder with its own solution. `MyApp/MyApp.slnx` opens in Rider, Visual Studio or VS Code, and the project itself is `MyApp/MyApp/MyApp.csproj`, so tests or an Aspire host can sit next to it later.

## Run it

```bash
dotnet watch --project MyApp
```

Open <http://localhost:3000>. You will see a heading, a **Count** button and a line of text that counts the clicks.

## Change something

Open `MyApp/Pages/Page.axaml`, change the text of the heading and save. The open tab updates in place, and it keeps what you typed, the scroll position and the focus. The same happens when you save a C# file. If .NET Hot Reload cannot apply an edit, the app restarts and the tab refreshes on its own.

## What the template gave you

| File | What it does |
| --- | --- |
| `Program.cs` | Registers your services and starts the app |
| `App.axaml` | The application's theme and shared styles |
| `Pages/Layout.axaml` | The header that wraps every page |
| `Pages/Page.axaml` | The page at `/` |
| `Pages/Page.Loader.cs` | Loads the data for that page |
| `Pages/about/Page.axaml` | The page at `/about` |
| `Pages/NotFound.axaml`, `Pages/Error.axaml` | The pages shown for a missing route and for an error |
| `Models/HomeModel.cs` | The view model of the home page |

`Program.cs` is short:

```csharp
using AiorosStarter.Models;

var builder = AiorosApplication.CreateBuilder(args);
builder.Services.AddSingleton<ClickCounter>();

var app = builder.Build();
app.MapAioros();
return app.RunAioros();
```

The home page is ordinary XAML. Its `x:DataType` says which view model it uses, and the bindings are compiled:

```xml
<UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:m="using:AiorosStarter.Models" x:DataType="m:HomeModel">
  <StackPanel Spacing="16">
    <TextBlock Classes="h1" Text="Welcome to Aioros" />
    <TextBlock TextWrapping="Wrap" Text="Edit Pages/Page.axaml and save: the browser updates in place." />
    <StackPanel Orientation="Horizontal" Spacing="12">
      <Button Classes="accent" Content="Count" Command="{Binding CountCommand}" />
      <TextBlock VerticalAlignment="Center" Text="{Binding Message}" />
    </StackPanel>
  </StackPanel>
</UserControl>
```

The view model is plain MVVM. It knows nothing about the web:

```csharp
using CommunityToolkit.Mvvm.Input;

namespace AiorosStarter.Models;

public sealed record Clicks(int Count);

public sealed partial class HomeModel(Clicks clicks, ClickCounter counter)
{
    public string Message { get; } = $"Clicked {clicks.Count} times.";

    [RelayCommand]
    private void Count() => counter.Increment();
}
```

## Add a page

Create `MyApp/Pages/hello/Page.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui" xmlns:a="https://aioros.dev" a:Page.Title="Hello">
  <TextBlock Classes="h1" Text="Hello from a new page" />
</UserControl>
```

Open <http://localhost:3000/hello>. The folder is the route, and there is nothing to register.

## Run it on the desktop

The same app runs as a native window, with the same pages, view models and services. There is no browser inside it:

```bash
dotnet run --project MyApp -- --desktop
```

## Build for production

The command line tool wraps the common tasks:

```bash
dotnet tool install -g Aioros.Cli --prerelease
```

Run these from the app's folder:

| Command | What it does |
| --- | --- |
| `aioros dev` | Runs the app with hot reload |
| `aioros dev --desktop` | Runs it as a native window |
| `aioros routes` | Lists the app's routes |
| `aioros build` | Publishes the app and precompiles its pages |
| `aioros start` | Runs the published app |

`aioros build` compiles every page to a render plan, so production serves them without starting the XAML engine. If a page cannot become a plan, the build fails and names the XAML element responsible.

## Next steps

Read [Concepts](concepts.md) to learn how routes, loaders and commands fit together, or run one of the samples in the repository, such as `samples/Lumen/Lumen`, a complete SaaS app.
