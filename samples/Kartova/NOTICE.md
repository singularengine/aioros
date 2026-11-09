# Notice

Kartova is ported from [markusfurtlehner/kartova](https://github.com/markusfurtlehner/kartova) (commit `db28c76`),
Copyright (c) 2026 Markus Furtlehner, under the MIT license in `LICENSE-Kartova.md`:

| Here | Source |
| --- | --- |
| `Kartova/` | `src/Kartova.App/` |
| `Kartova/Views/MainView.axaml`, `MainView.axaml.cs` | `src/Kartova.App/Views/MainWindow.axaml`, `MainWindow.axaml.cs` |
| `Kartova.Core/` | `src/Kartova.Core/` |

Changes, in two steps:

1. Upgrade to .NET 10 and Avalonia 12: package versions, `Avalonia.Diagnostics` dropped, `Watermark` renamed
   `PlaceholderText`, `SystemDecorations` and `ExtendClientAreaChromeHints` replaced by `WindowDecorations`, the
   clipboard's `SetTextAsync` extension and `Bitmap.Save` with encoder options.
2. Aioros: `Program.cs` is the Aioros builder (the command-line mode is kept); the window became `Views/MainView`, a
   user control placed by `Pages/Page.axaml` (native only, `a:Page.Platforms="Desktop"`), without its own caption
   buttons, drag handling and placement, since the window is the framework's; the window's size, the settings saved on
   close and the folder given on the command line moved from `App` and the window to `MainView`; `Pages/about/Page.axaml`
   is a web page made from the About dialog. The tests of `tests/Kartova.Core.Tests` are not ported.
