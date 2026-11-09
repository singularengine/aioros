using Aioros.Markup;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aioros.Avalonia.Tests.Models;

/// <summary>
/// A library's dialog marked for the browser, as Aioros.Cosmo's <c>c:Dialog</c>: <c>a:Web.Modal="IsOpen"</c>, its mask
/// and surface (a title, a description, a field bound to the model's <c>Name</c>, a <c>Save</c> button bound to the
/// model's command and a <c>Close</c> button that closes it in the browser) added to the page's <see cref="PromptHost"/>
/// while it is open, each marked <c>a:Web.ModalOf</c> it.
/// </summary>
public sealed class MarkedModal : Control
{
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<MarkedModal, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    private Border? _mask;
    private Border? _surface;

    static MarkedModal()
    {
        IsOpenProperty.Changed.AddClassHandler<MarkedModal>((modal, _) => modal.Toggle(modal.IsOpen || Web.GetShows(modal)));
        Web.ShowsProperty.Changed.AddClassHandler<MarkedModal>((modal, _) => modal.Toggle(modal.IsOpen || Web.GetShows(modal)));
    }

    public MarkedModal()
    {
        IsVisible = false;
        Web.SetModal(this, nameof(IsOpen));
        Web.SetLightDismiss(this, true);
        AttachedToVisualTree += (_, _) => Toggle(OpensAttached && (IsOpen || Web.GetShows(this)));
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>
    /// Whether the mask fades in over 150 ms and the surface over 200 ms as they are shown, and each, while it is
    /// <c>:closing</c>, leaves at opacity 0 (the surface over 100 ms), as Radix's overlay and content animate in and out.
    /// </summary>
    public bool Moving { get; set; }

    /// <summary>Whether the dialog, bound open before it is in the page, opens once it is attached, as Aioros.Cosmo's modals open from the start.</summary>
    public bool OpensAttached { get; set; }

    private static void Move(Border part, int entering, int leaving)
    {
        part.Styles.Add(new global::Avalonia.Styling.Style(selector => global::Avalonia.Styling.Selectors.OfType(selector, typeof(Border)))
        {
            Setters = { new global::Avalonia.Styling.Setter(TransitionsProperty, new global::Avalonia.Animation.Transitions { new global::Avalonia.Animation.DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(entering) } }) },
        });
        part.Styles.Add(new global::Avalonia.Styling.Style(selector => global::Avalonia.Styling.Selectors.Class(global::Avalonia.Styling.Selectors.OfType(selector, typeof(Border)), ":closing"))
        {
            Setters =
            {
                new global::Avalonia.Styling.Setter(OpacityProperty, 0d),
                new global::Avalonia.Styling.Setter(TransitionsProperty, new global::Avalonia.Animation.Transitions { new global::Avalonia.Animation.DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(leaving) } }),
            },
        });
        Fade.SetEnter(part, true);
    }

    private void Toggle(bool open)
    {
        if (TopLevel.GetTopLevel(this)?.GetVisualDescendants().OfType<PromptHost>().FirstOrDefault() is not { } host)
        {
            return;
        }

        if (open && _surface is null)
        {
            _mask = new Border { Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)) };
            _mask.PointerPressed += (_, _) => SetCurrentValue(IsOpenProperty, false);
            var close = new Button { Content = "Close" };
            Web.SetCloses(close, true);
            close.Click += (_, _) => SetCurrentValue(IsOpenProperty, false);
            _surface = new Border
            {
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 425,
                Padding = new Thickness(24),
                [!DataContextProperty] = this[!DataContextProperty],
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = "Edit profile", FontWeight = FontWeight.SemiBold },
                        new TextBlock { Text = "Make changes to your profile here." },
                        new TextBox { [!TextBox.TextProperty] = new Binding("Name") },
                        new Button { Content = "Save", [!Button.CommandProperty] = new Binding("SaveCommand") },
                        close,
                    },
                },
            };
            _surface.AddHandler(KeyDownEvent, (_, key) =>
            {
                if (key.Key == Key.Escape)
                {
                    SetCurrentValue(IsOpenProperty, false);
                }
            }, RoutingStrategies.Bubble, handledEventsToo: true);
            if (Moving)
            {
                Move(_mask, 150, 150);
                Move(_surface, 200, 100);
            }

            Web.SetModalOf(_mask, this);
            Web.SetModalOf(_surface, this);
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

public sealed partial class Editing : ObservableObject
{
    [ObservableProperty]
    public partial bool Open { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = "Ada";

    public int Saved { get; private set; }

    public int Shown { get; private set; }

    [RelayCommand]
    private void Show()
    {
        Shown++;
        Open = true;
    }

    [RelayCommand]
    private void Save()
    {
        Saved++;
        Open = false;
    }
}
