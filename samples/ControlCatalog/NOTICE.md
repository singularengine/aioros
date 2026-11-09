# Notice

The pages in `ControlCatalog/Pages` are ported from Avalonia's ControlCatalog sample, tag `12.1.3`
(commit `8eeda4f6f546165b3f72e63c9f42247abb306905`), under the MIT license in `LICENSE-Avalonia.md`:

| Page | Source |
| --- | --- |
| `Pages/border/Page.axaml` | `samples/ControlCatalog/Pages/BorderPage.xaml` |
| `Pages/relativepanel/Page.axaml` | `samples/ControlCatalog/Pages/RelativePanelPage.axaml` |
| `Pages/textblock/Page.axaml` | `samples/ControlCatalog/Pages/TextBlockPage.xaml` |
| `App.axaml` | `samples/ControlCatalog/App.xaml` (theme dictionaries, text styles) and `ScrollPage.xaml` |
| `Assets/maple-leaf-888807_640.jpg` | `samples/ControlCatalog/Assets/maple-leaf-888807_640.jpg` |

Changes: `x:Class` removed (Aioros pages have no code-behind); the `ScrollPage` control theme is inlined in
`App.axaml` instead of a `ResourceInclude`; the TextBlock page leaves out the Source Sans Pro custom font block (the
font files are not copied).
