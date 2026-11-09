using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;

namespace Aioros.Avalonia.Tests.Pages;

public sealed partial class ThrowsLater : UserControl
{
    public const string Message = "The later page threw after its answer.";

    public ThrowsLater()
    {
        InitializeComponent();
        ThrowWhenReleased();
    }

    public static TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [SuppressMessage("Usage", "VSTHRD100", Justification = "The page stands for fire-and-forget code-behind whose exception the server must contain.")]
    [SuppressMessage("Usage", "S3168", Justification = "The page stands for fire-and-forget code-behind whose exception the server must contain.")]
    private static async void ThrowWhenReleased()
    {
        await Release.Task;
        throw new InvalidOperationException(Message);
    }
}
