using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aioros.Avalonia.Tests.Models;

public sealed class PromptHost : Panel
{
    public static PromptHost? Current { get; private set; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Current = this;
        if (DataContext is IPromptOwner owner)
        {
            owner.Host = this;
        }
    }
}

public sealed partial class Asking : ObservableObject
{
    [RelayCommand]
    private static async Task AskAsync()
    {
        PromptHost.Current!.Children.Add(new Border { Width = 10, Height = 10 });
        await new TaskCompletionSource().Task;
    }
}

public sealed partial class Question : ObservableObject
{
    private readonly TaskCompletionSource<string?> _answer = new();

    [ObservableProperty]
    public partial string? Text { get; set; }

    [ObservableProperty]
    public partial int Nudges { get; set; }

    public Task<string?> Answer => _answer.Task;

    [RelayCommand]
    private void Accept() => _answer.TrySetResult($"{Text} ({Nudges})");

    [RelayCommand]
    private void Nudge() => Nudges++;

    public Control View() => new Border
    {
        DataContext = this,
        Child = new StackPanel
        {
            Children =
            {
                new TextBox { [!TextBox.TextProperty] = new Binding(nameof(Text)) { Mode = BindingMode.TwoWay } },
                new TextBlock { [!TextBlock.TextProperty] = new Binding(nameof(Nudges)) { StringFormat = "Nudged {0}" } },
                new Button { Content = "Nudge", [!Button.CommandProperty] = new Binding(nameof(NudgeCommand)) },
                new Button { Content = "OK", [!Button.CommandProperty] = new Binding(nameof(AcceptCommand)) },
            },
        },
    };
}

public sealed partial class Confirming : ObservableObject, IPromptOwner
{
    public Panel? Host { get; set; }

    [ObservableProperty]
    public partial string? Name { get; set; }

    [ObservableProperty]
    public partial string? Answer { get; set; }

    public int Asked { get; private set; }

    public int Answered { get; private set; }

    [RelayCommand]
    private async Task AskAsync()
    {
        Asked++;
        var question = new Question();
        var prompt = question.View();
        Host!.Children.Add(prompt);
        var answer = await question.Answer;
        Host.Children.Remove(prompt);
        Answered++;
        Answer = Name + ": " + answer;
    }
}

public sealed class NoticeHost : Panel
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is Notifying owner)
        {
            owner.Manager = new WindowNotificationManager(TopLevel.GetTopLevel(this)) { Position = NotificationPosition.TopRight };
        }
    }
}

public sealed partial class Notifying : ObservableObject, IPromptOwner
{
    private readonly TaskCompletionSource _closed = new();

    public WindowNotificationManager? Manager { get; set; }

    public Panel? Host { get; set; }

    public int Notified { get; private set; }

    [RelayCommand]
    private void Notify()
    {
        Notified++;
        Manager!.Show(new Notification("Saved", "The draft is saved"));
    }

    public int Rested { get; private set; }

    [RelayCommand]
    private void Rest() => Rested++;

    [RelayCommand]
    private void Close() => _closed.TrySetResult();

    [RelayCommand]
    private async Task AskAsync()
    {
        var prompt = new StackPanel
        {
            Children =
            {
                new Button { Content = "Notify", [!Button.CommandProperty] = new Binding(nameof(NotifyCommand)) },
                new Button { Content = "Close", [!Button.CommandProperty] = new Binding(nameof(CloseCommand)) },
            },
        };
        Host!.Children.Add(prompt);
        await _closed.Task;
        Host.Children.Remove(prompt);
    }
}

public sealed class ClickedButton : Button
{
    public ClickedButton() => Click += (_, click) =>
    {
        if (DataContext is Clicking clicking)
        {
            clicking.Clicks++;
        }

        click.Handled = Handles;
    };

    public bool Handles { get; set; }

    protected override Type StyleKeyOverride => typeof(Button);
}

public sealed partial class Clicking : ObservableObject
{
    public int Clicks { get; set; }

    public int Ran { get; private set; }

    [RelayCommand]
    private void Run() => Ran++;
}

public interface IPromptOwner
{
    Panel? Host { get; set; }
}
