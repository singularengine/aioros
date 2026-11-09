using System.Collections.Immutable;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aioros.Avalonia.Tests.Models;

/// <summary>A link of a site's navigation menu.</summary>
public sealed record MenuLink(string Title, string Description, string Page);

/// <summary>A site's menu model: links from the model, the page a chosen link moves to.</summary>
public sealed partial class SiteMenu : ObservableObject
{
    public ImmutableArray<MenuLink> Links { get; } =
    [
        new("Installation", "How to install dependencies and structure your app.", "/docs/installation"),
        new("Typography", "Styles for headings, paragraphs, lists...etc", "/docs/typography"),
    ];

    [ObservableProperty]
    public partial string CurrentPage { get; set; } = "/";

    [RelayCommand]
    private void Navigate(string page) => CurrentPage = page;
}

/// <summary>
/// Aioros.Cosmo's navigation menu (Radix's NavigationMenu): a row of triggers 4 px apart; a trigger opens its content when
/// pressed or after the pointer rests on it for <see cref="DelayDuration"/>, moving onto another trigger while one is open
/// opens that one at once, and leaving both the trigger and its content closes it after <see cref="CloseDelay"/>.
/// </summary>
public sealed class NavigationMenu : StackPanel
{
    private ITimer? _timer;

    public NavigationMenu()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 4;
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Navigation);
        AutomationProperties.SetName(this, "Main");
    }

    public TimeSpan DelayDuration { get; set; } = TimeSpan.FromMilliseconds(200);

    public TimeSpan CloseDelay { get; set; } = TimeSpan.FromMilliseconds(150);

    public TimeProvider Time { get; set; } = TimeProvider.System;

    public NavigationMenuTrigger? OpenTrigger => Children.OfType<NavigationMenuTrigger>().FirstOrDefault(trigger => trigger.Flyout?.IsOpen == true);

    internal void Leave()
    {
        if (OpenTrigger is { } open)
        {
            Schedule(CloseDelay, () =>
            {
                if (!open.IsPointerOver)
                {
                    open.Flyout?.Hide();
                }
            });
        }
    }

    internal void Enter(NavigationMenuTrigger trigger)
    {
        if (trigger.Flyout is not { } flyout || flyout.IsOpen)
        {
            Cancel();
            return;
        }

        if (OpenTrigger is { } open)
        {
            Cancel();
            open.Flyout?.Hide();
            flyout.ShowAt(trigger);
            return;
        }

        Schedule(DelayDuration, () =>
        {
            if (trigger.IsPointerOver && !flyout.IsOpen)
            {
                flyout.ShowAt(trigger);
            }
        });
    }

    private void Schedule(TimeSpan delay, Action action)
    {
        Cancel();
        _timer = Time.CreateTimer(_ => Dispatcher.UIThread.Post(action), null, delay, Timeout.InfiniteTimeSpan);
    }

    private void Cancel()
    {
        _timer?.Dispose();
        _timer = null;
    }
}

/// <summary>Aioros.Cosmo's navigation menu trigger: a button whose flyout, its content, opens under its start, 6 px below.</summary>
public sealed class NavigationMenuTrigger : Button
{
    protected override Type StyleKeyOverride => typeof(NavigationMenuTrigger);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FlyoutProperty && change.NewValue is Flyout flyout)
        {
            flyout.Placement = PlacementMode.BottomEdgeAlignedLeft;
            flyout.VerticalOffset = 6;
        }
    }

    protected override void OnClick()
    {
        if (Flyout?.IsOpen == true)
        {
            Flyout.Hide();
            return;
        }

        base.OnClick();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        (Parent as NavigationMenu)?.Enter(this);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        (Parent as NavigationMenu)?.Leave();
    }
}
