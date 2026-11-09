# Notice

This app is ported from the AdvancedToDoList sample of Avalonia.Samples
(`src/Avalonia.Samples/CompleteApps/AdvancedToDoList`, commit `daa400031456dfed0987ccc848d8341602ef0dad`, Avalonia
12.1.3), Copyright (c) 2025 AvaloniaUI OÜ, under the MIT license in `LICENSE-Avalonia.Samples.md`. The phosphor icon
geometry in `AdvancedToDoList/Assets/Icons.axaml` comes from Phosphor Icons (MIT), as the original notes.

| Here | Source |
| --- | --- |
| `SharedControls/` | `SharedControls/` (the HamburgerMenu and the overlay dialogs stay, unused) |
| `AdvancedToDoList/Models`, `Messages`, `Helper`, `DataTemplates`, `Properties`, `ViewModels`, `Services`, `Assets`, `App.axaml` | `AdvancedToDoList/` |
| `AdvancedToDoList/Services/DesktopDatabaseService.cs` | `AdvancedToDoList.Desktop/Services/` |
| `AdvancedToDoList/Pages/Layout.axaml` | `Views/MainView.axaml` and `Views/MainWindow.axaml` (the HamburgerMenu as routes and a layout) |
| `AdvancedToDoList/Pages/Page.axaml` | `Views/ManageToDoItemsView.axaml` |
| `AdvancedToDoList/Pages/categories/Page.axaml` | `Views/ManageCategoriesView.axaml` |
| `AdvancedToDoList/Pages/settings/Page.axaml` | `Views/SettingsView.axaml` |
| `AdvancedToDoList/Pages/items/[id]/Page.axaml` | `Views/EditToDoItemView.axaml` (an overlay dialog, now a page) |
| `AdvancedToDoList/Pages/categories/[id]/Page.axaml` | `Views/EditCategoryView.axaml` (an overlay dialog, now a page) |
| `AdvancedToDoList/Pages/confirm/[action]/[id]/Page.axaml` | The Yes/No overlay dialogs (`SharedControls/Controls/Dialogs`) |

Changes: the view models take their data from loaders and their services from DI (`App.Services` and the static
`DatabaseHelper` are gone from them), navigate through `IAppNavigator` where they opened dialogs, and use a scoped
`IMessenger`; data access is behind server functions (`IToDoItemService`, `ICategoryService`,
`IToDoListDataService`); the browser
(WASM), Android, iOS and test projects, the design-time data and the Serilog setup are not ported.
