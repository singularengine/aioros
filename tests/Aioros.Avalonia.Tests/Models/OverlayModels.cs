using System.Reflection;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aioros.Avalonia.Tests.Models;

/// <summary>A library's hover delays, declared as attached properties of those names (as a hover card declares them).</summary>
public static class Hover
{
    public static readonly AttachedProperty<int> OpenDelayProperty = AvaloniaProperty.RegisterAttached<Control, int>("OpenDelay", typeof(Hover), 700);

    public static readonly AttachedProperty<int> CloseDelayProperty = AvaloniaProperty.RegisterAttached<Control, int>("CloseDelay", typeof(Hover), 300);

    public static int GetOpenDelay(Control control) => control.GetValue(OpenDelayProperty);

    public static void SetOpenDelay(Control control, int value) => control.SetValue(OpenDelayProperty, value);

    public static int GetCloseDelay(Control control) => control.GetValue(CloseDelayProperty);

    public static void SetCloseDelay(Control control, int value) => control.SetValue(CloseDelayProperty, value);
}

/// <summary>A surface that enters as an overlay does: from opacity 0 and 95%, its transitions started once it is shown.</summary>
public sealed class Entering : Border
{
    public Entering()
    {
        Opacity = 0;
        RenderTransform = TransformOperations.Parse("scale(0.95)");
        Transitions =
        [
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(150) },
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(150) },
        ];
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(() =>
        {
            Opacity = 1;
            RenderTransform = TransformOperations.Identity;
        });
    }
}

/// <summary>A border that enters from opacity 0 in a millisecond as it is shown, so any move of its clock ends its entrance.</summary>
public sealed class QuickEntering : Border
{
    public QuickEntering()
    {
        Opacity = 0;
        Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(1) }];
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(() => Opacity = 1);
    }
}

/// <summary>
/// A border that moves the platform's animation clock ten seconds on once it is shown, after the controls before it
/// began their transitions, as the platform's render ticks move it with real time while a long page is realized.
/// </summary>
public sealed class Aging : Border
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(
            () =>
            {
                var clock = typeof(Animatable).Assembly.GetType("Avalonia.Animation.Clock")!.GetProperty("GlobalClock", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                var pulse = clock.GetType().GetMethod("Pulse", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
                var now = TimeSpan.FromMilliseconds(Environment.TickCount64);
                pulse.Invoke(clock, [now]);
                pulse.Invoke(clock, [now + TimeSpan.FromSeconds(10)]);
            },
            DispatcherPriority.Background);
    }
}

/// <summary>A part that fades in each time it is shown, from opacity 0, as a theme's entrance does.</summary>
public static class Fade
{
    public static readonly AttachedProperty<bool> EnterProperty = AvaloniaProperty.RegisterAttached<Control, bool>("Enter", typeof(Fade));

    /// <summary>How many pixels up the part enters from as well, none by default.</summary>
    public static readonly AttachedProperty<double> LiftProperty = AvaloniaProperty.RegisterAttached<Control, double>("Lift", typeof(Fade));

    static Fade() => EnterProperty.Changed.AddClassHandler<Control>((control, change) =>
    {
        if (change.GetNewValue<bool>())
        {
            control.AttachedToVisualTree += (_, _) =>
            {
                using (control.SetValue(Animatable.TransitionsProperty, null, global::Avalonia.Data.BindingPriority.Animation))
                {
                    control.Opacity = 0;
                    if (GetLift(control) > 0)
                    {
                        control.RenderTransform = TransformOperations.Parse($"translate(0px,{-GetLift(control)}px)");
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    control.ClearValue(Visual.OpacityProperty);
                    control.ClearValue(Visual.RenderTransformProperty);
                });
            };
        }
    });

    public static double GetLift(Control control) => control.GetValue(LiftProperty);

    public static void SetLift(Control control, double value) => control.SetValue(LiftProperty, value);

    public static bool GetEnter(Control control) => control.GetValue(EnterProperty);

    public static void SetEnter(Control control, bool value) => control.SetValue(EnterProperty, value);
}

public sealed partial class MenuPicks : ObservableObject
{
    [ObservableProperty]
    public partial bool Wrap { get; set; } = true;
}
