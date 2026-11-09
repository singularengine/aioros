using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace Aioros.Avalonia.Tests.Pages;

public sealed partial class Failing : UserControl
{
    public const string Message = "The failing page's handler threw.";

    public Failing() => InitializeComponent();

    [SuppressMessage("Usage", "VSTHRD100", Justification = "The page is Semi.Avalonia's /palette: an async void handler in code-behind whose exception the server must contain.")]
    protected override async void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        await Dispatcher.UIThread.InvokeAsync(Fail);
    }

    private static void Fail() => throw new InvalidOperationException(Message);
}
