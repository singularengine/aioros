using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aioros.Avalonia.Tests.Models;

/// <summary>
/// A dialog as a component library writes one (Radix's Dialog in Avalonia): while its bound <see cref="IsOpen"/> is true,
/// a mask and a centered surface cover the page's <see cref="PromptHost"/>; Escape in the surface and, with light
/// dismiss, a press on the mask set it false; the surface focuses its text box.
/// </summary>
public sealed class Modal : Control
{
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<Modal, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> LightDismissProperty = AvaloniaProperty.Register<Modal, bool>(nameof(LightDismiss), true);

    public static readonly StyledProperty<bool> AlertProperty = AvaloniaProperty.Register<Modal, bool>(nameof(Alert));

    public static readonly StyledProperty<bool> FadingProperty = AvaloniaProperty.Register<Modal, bool>(nameof(Fading));

    private Border? _mask;
    private Border? _surface;

    static Modal() => IsOpenProperty.Changed.AddClassHandler<Modal>((modal, change) => modal.Toggle(change.GetNewValue<bool>()));

    public Modal() => IsVisible = false;

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public bool LightDismiss
    {
        get => GetValue(LightDismissProperty);
        set => SetValue(LightDismissProperty, value);
    }

    public bool Alert
    {
        get => GetValue(AlertProperty);
        set => SetValue(AlertProperty, value);
    }

    /// <summary>Whether the mask fades in over 150 ms as it is shown, as Radix's overlay's <c>fade-in-0</c>.</summary>
    public bool Fading
    {
        get => GetValue(FadingProperty);
        set => SetValue(FadingProperty, value);
    }

    private void Toggle(bool open)
    {
        if (PromptHost.Current is not { } host)
        {
            return;
        }

        if (open && _surface is null)
        {
            _mask = new Border { Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)) };
            _mask.PointerPressed += (_, _) =>
            {
                if (LightDismiss)
                {
                    SetCurrentValue(IsOpenProperty, false);
                }
            };
            if (Fading)
            {
                _mask.Transitions = [new global::Avalonia.Animation.DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(150) }];
                Fade.SetEnter(_mask, true);
            }

            var field = new TextBox { Text = "Ada" };
            field.Loaded += (_, _) => field.Focus();
            _surface = new Border
            {
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 425,
                Padding = new Thickness(24),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = "Edit profile", FontWeight = FontWeight.SemiBold },
                        new TextBlock { Text = "Make changes to your profile here." },
                        field,
                        new Button { Content = "Close", [!Button.CommandProperty] = new Binding("HideCommand") },
                    },
                },
            };
            if (Alert)
            {
                AutomationProperties.SetLiveSetting(_surface, AutomationLiveSetting.Assertive);
                ((StackPanel)_surface.Child).Children.Add(new Button { Content = "Cancel", IsCancel = true, [!Button.CommandProperty] = new Binding("HideCommand") });
            }

            _surface.AddHandler(KeyDownEvent, (_, key) =>
            {
                if (key.Key == Key.Escape)
                {
                    SetCurrentValue(IsOpenProperty, false);
                }
            }, RoutingStrategies.Bubble, handledEventsToo: true);
            host.Children.Add(_mask);
            host.Children.Add(_surface);
        }
        else if (!open && _surface is not null)
        {
            host.Children.Remove(_mask!);
            host.Children.Remove(_surface);
            (_mask, _surface) = (null, null);
        }
    }
}

public sealed partial class Opening : ObservableObject
{
    [ObservableProperty]
    public partial bool Open { get; set; }

    public int Hidden { get; private set; }

    [RelayCommand]
    private void Show() => Open = true;

    [RelayCommand]
    private void Hide()
    {
        Hidden++;
        Open = false;
    }
}

/// <summary>A notification manager whose theme stands its cards in a grid at the bottom right (a deck of toasts).</summary>
public sealed class DeckHost : Panel
{
    public ControlTheme? Deck { get; set; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is Toasting owner)
        {
            owner.Manager = new WindowNotificationManager(TopLevel.GetTopLevel(this)) { Position = NotificationPosition.BottomRight, Theme = Deck };
        }
    }
}

/// <summary>A notification manager with its own theme (a deck) whose cards take <see cref="CardTheme"/>, as a library's toaster.</summary>
public sealed class DeckCardsHost : Panel
{
    public ControlTheme? Deck { get; set; }

    public ControlTheme? CardTheme { get; set; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is Toasting owner)
        {
            var manager = new WindowNotificationManager(TopLevel.GetTopLevel(this)) { Position = NotificationPosition.BottomRight, Theme = Deck };
            manager.Styles.Add(new Style(selector => selector.OfType<NotificationCard>()) { Setters = { new Setter(ThemeProperty, CardTheme) } });
            owner.Manager = manager;
        }
    }
}

public sealed class ToastHost : Panel
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is Toasting owner)
        {
            owner.Manager = new WindowNotificationManager(TopLevel.GetTopLevel(this)) { Position = NotificationPosition.TopRight };
        }
    }
}

/// <summary>A toaster whose cards take <see cref="CardTheme"/>, as a library's toaster gives its cards their theme.</summary>
public sealed class RisingHost : Panel
{
    public ControlTheme? CardTheme { get; set; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is Toasting owner)
        {
            var manager = new WindowNotificationManager(TopLevel.GetTopLevel(this)) { Position = NotificationPosition.BottomRight };
            manager.Styles.Add(new Style(selector => selector.OfType<NotificationCard>()) { Setters = { new Setter(ThemeProperty, CardTheme) } });
            owner.Manager = manager;
        }
    }
}

public sealed partial class Toasting : ObservableObject
{
    public WindowNotificationManager? Manager { get; set; }

    public int Shown { get; private set; }

    public int Undone { get; private set; }

    [RelayCommand]
    private void Toast()
    {
        Shown++;
        Manager!.Show(new Notification("Saved", "The draft is saved"));
        Manager.Show(new Notification("Kept", "Until it is closed", expiration: TimeSpan.Zero));
    }

    [RelayCommand]
    private void Act()
    {
        Shown++;
        Manager!.Show(new Button { Content = "Undo", Command = UndoCommand });
    }

    [RelayCommand]
    private void Undo() => Undone++;

    [RelayCommand]
    private void Stack()
    {
        Shown++;
        Manager!.Show(new TextBlock { Text = "The oldest card" });
        Manager.Show(new TextBlock { Text = "Behind two" });
        Manager.Show(new TextBlock { Text = "Behind one" });
        Manager.Show(new TextBlock { Text = "The newest card\nits description on two lines" });
    }
}
