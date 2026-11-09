using System.Globalization;
using System.Collections.Immutable;
using Aioros.Avalonia.Forms;
using Aioros.Avalonia.Html.Writers;
using Avalonia.Controls;

namespace Aioros.Avalonia.Html.Families;

internal static class NavigationFamily
{
    public static ImmutableArray<IControlWriter> Writers { get; } =
    [
        new ControlWriter<SplitView>(CompositeWriters.WriteSplitView),
        new ControlWriter<Expander>(CompositeWriters.WriteExpander),
        new ControlWriter<Menu>(MenuWriters.WriteMenu),
        new ControlWriter<FlyoutPresenter>(FlyoutWriters.WritePresenter),
        new ControlWriter<DataValidationErrors>(OverlayWriters.WriteValidation),
        new ControlWriter<CommandBar>(CommandBarWriters.WriteBar),
        new ControlWriter<CommandBarButton>(CommandBarWriters.WriteButton),
        new ControlWriter<CommandBarToggleButton>(CommandBarWriters.WriteToggle),
        new ControlWriter<CommandBarSeparator>(CommandBarWriters.WriteSeparator),
        new ControlWriter<TransitioningContentControl>(PageWriters.WriteTransitioning),
        new ControlWriter<RefreshContainer>(PageWriters.WriteRefreshContainer),
        new ControlWriter<ContentPage>(PageWriters.WriteContentPage),
        new ControlWriter<NavigationPage>(PageWriters.WriteNavigationPage),
        new ControlWriter<TabbedPage>(PageWriters.WriteTabbedPage),
        new ControlWriter<DrawerPage>(PageWriters.WriteDrawerPage),
        new ControlWriter<CarouselPage>(PageWriters.WriteCarouselPage),
        new ControlWriter<PageNavigationHost>(PageWriters.WriteHost),
    ];

    public static ImmutableArray<IFieldBinder> Binders { get; } =
    [
        new FieldBinder<CommandBarToggleButton>((toggle, value) => toggle.IsChecked = value is not null),
        new FieldBinder<Expander>(Html.Writers.ExpanderWriters.Bind),
        new FieldBinder<TabbedPage>((tabbed, value) => tabbed.SelectedIndex = int.TryParse(value, CultureInfo.InvariantCulture, out var index) ? index : tabbed.SelectedIndex),
    ];
}
