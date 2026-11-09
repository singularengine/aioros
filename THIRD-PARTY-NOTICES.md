# Third-party notices

Aioros includes or depends on the following third-party work.

| Component | Where it is used | License |
|---|---|---|
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) | The UI framework the XAML runs on (`Aioros.Avalonia`) | MIT |
| [Avalonia ControlCatalog](https://github.com/AvaloniaUI/Avalonia/tree/12.1.3/samples/ControlCatalog) | Pages ported in `samples/ControlCatalog` (see its `NOTICE.md`) | MIT |
| [OxyPlot](https://github.com/oxyplot/oxyplot) (`OxyPlot.Core`) | Chart layout for `a:Chart` (`src/Adapters/Aioros.Avalonia/Charts`) | MIT |
| [Lucide](https://lucide.dev) | Icon geometry in `src/Adapters/Aioros.Avalonia/Controls/IconSet.cs` (shipped in the `Aioros.Avalonia` package) | ISC (notice below) |
| [d3-shape](https://github.com/d3/d3-shape) 3.2.0 | The monotone curves (`curveMonotoneX`/`curveMonotoneY`, their slope tangents) ported into the morph module (`src/Targets/Aioros.Targets.Ssr/Documents/MorphScript.cs`) | ISC, Copyright 2010-2022 Mike Bostock (notice below) |
| [Recharts](https://github.com/recharts/recharts) 2.15.4 | The sector path (`getSectorPath`) ported into the morph module (`src/Targets/Aioros.Targets.Ssr/Documents/MorphScript.cs`) | MIT, Copyright (c) 2015-present recharts (notice below) |
| [Inter](https://rsms.me/inter/) 3.019 | The web font (`src/Targets/Aioros.Targets.Ssr/Fonts`: the static files `Avalonia.Fonts.Inter` embeds, subset to Latin and Latin Extended as woff2) and the chart text metrics generated from them (`Charts/InterMetrics.cs`) | SIL Open Font License 1.1 (`Fonts/Inter-LICENSE.txt`) |
| Cosmo Symbols (from Adwaita Sans 50.0, GNOME's adwaita-fonts, after Inter) | A test font (`tests/Aioros.Avalonia.Tests/Assets/Fonts/CosmoSymbols-Regular.ttf`): seven keyboard symbols subset from Adwaita Sans and renamed, as Aioros.Cosmo ships them | SIL Open Font License 1.1 (`CosmoSymbols-OFL.txt` beside it) |
| Cosmo Emoji (from Noto Color Emoji, Google) | A test font (`tests/Aioros.Avalonia.Tests/Assets/Fonts/CosmoEmoji-Regular.ttf`): four emoji subset from Noto Color Emoji with their color bitmaps (CBDT) and renamed, as Aioros.Cosmo ships them | SIL Open Font License 1.1 (`CosmoEmoji-OFL.txt` beside it) |
| [AsyncImageLoader.Avalonia](https://github.com/AvaloniaUtils/AsyncImageLoader.Avalonia) | Images from URLs (`ImageLoader.Source`) on the desktop; the web writes them as `<img>` | MIT |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | Commands in the adapter | MIT |
| [ReactiveUI](https://github.com/reactiveui/ReactiveUI) 26.0.1 | The optional ReactiveUI adapter (`src/Adapters/Aioros.ReactiveUI`) | MIT |
| [ReactiveUI.Samples](https://github.com/reactiveui/ReactiveUI.Samples) (`avalonia/suspension`) | Ported as `samples/ReactiveRouting` (view models and views, Copyright (c) .NET Foundation and Contributors) | MIT |
| [Kartova](https://github.com/markusfurtlehner/kartova) | Ported as `samples/Kartova` (Copyright (c) 2026 Markus Furtlehner; see its `NOTICE.md` and `LICENSE-Kartova.md`) | MIT |
| [Avalonia.Samples](https://github.com/AvaloniaUI/Avalonia.Samples) (`CompleteApps/AdvancedToDoList`) | Ported as `samples/AdvancedToDoList` (view models, views, shared controls; Copyright (c) 2025 AvaloniaUI OÜ, see its `NOTICE.md`) | MIT |
| [OpenIddict](https://github.com/openiddict/openiddict-core) 7.7.2 (`OpenIddict.Core`, `.Server.AspNetCore`, `.Server.DataProtection`, `.Validation.AspNetCore`, `.Validation.DataProtection`, `.Validation.ServerIntegration`) | The installed app's authorization server (`src/Targets/Aioros.Targets.Ssr/SignIn`) | Apache-2.0 |
| [System.Security.Cryptography.ProtectedData](https://github.com/dotnet/runtime) | DPAPI for the installed app's tokens on Windows (`src/Aioros.Runtime/SignIn/TokenStores.cs`) | MIT |
| [Velopack](https://github.com/velopack/velopack) 1.2.161 (`Velopack`, and the `vpk` tool `aioros build --installer` runs) | The installed app's installers and updates (`src/Targets/Aioros.Targets.Desktop/Handoff/AppUpdates.cs`) | MIT |

## Lucide

ISC License. Copyright (c) for portions of Lucide are held by Cole Bemis 2013-2022 as part of Feather (MIT). All other
copyright (c) for Lucide are held by Lucide Contributors 2022.

Permission to use, copy, modify, and/or distribute this software for any purpose with or without fee is hereby granted,
provided that the above copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF
THIS SOFTWARE.

## d3-shape

```
Copyright 2010-2022 Mike Bostock

Permission to use, copy, modify, and/or distribute this software for any purpose
with or without fee is hereby granted, provided that the above copyright notice
and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH
REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND
FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS
OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER
TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF
THIS SOFTWARE.
```

## Recharts

```
The MIT License (MIT)

Copyright (c) 2015-present recharts

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
