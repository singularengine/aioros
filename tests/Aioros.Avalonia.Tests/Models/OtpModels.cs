using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Aioros.Avalonia.Tests.Models;
/// <summary>A one-time code input of a library, Aioros.Cosmo's <c>c:InputOtp</c>: its slots a list its template shows, built in its code from its text.</summary>
public sealed record OtpSlot(string Character, bool IsActive, bool IsFirst, bool IsLast, bool HasSeparator);
[TemplatePart("PART_TextBox", typeof(TextBox))]
public sealed class InputOtp : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<InputOtp, string?>(nameof(Text), defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<int> LengthProperty = AvaloniaProperty.Register<InputOtp, int>(nameof(Length), 6);
    public static readonly StyledProperty<int> GroupSizeProperty = AvaloniaProperty.Register<InputOtp, int>(nameof(GroupSize));
    public static readonly DirectProperty<InputOtp, IReadOnlyList<OtpSlot>> SlotsProperty =
        AvaloniaProperty.RegisterDirect<InputOtp, IReadOnlyList<OtpSlot>>(nameof(Slots), otp => otp.Slots);

    private IReadOnlyList<OtpSlot> _slots = [];

    static InputOtp()
    {
        FocusableProperty.OverrideDefaultValue<InputOtp>(false);
        TextProperty.Changed.AddClassHandler<InputOtp>((otp, _) => otp.Refresh());
        LengthProperty.Changed.AddClassHandler<InputOtp>((otp, _) => otp.Refresh());
        GroupSizeProperty.Changed.AddClassHandler<InputOtp>((otp, _) => otp.Refresh());
        IsKeyboardFocusWithinProperty.Changed.AddClassHandler<InputOtp>((otp, _) => otp.Refresh());
    }
    public InputOtp() => Refresh();
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
    public int Length
    {
        get => GetValue(LengthProperty);
        set => SetValue(LengthProperty, value);
    }
    public int GroupSize
    {
        get => GetValue(GroupSizeProperty);
        set => SetValue(GroupSizeProperty, value);
    }
    public IReadOnlyList<OtpSlot> Slots
    {
        get => _slots;
        private set => SetAndRaise(SlotsProperty, ref _slots, value);
    }

    private void Refresh()
    {
        var text = Text ?? string.Empty;
        var length = Math.Max(1, Length);
        var group = GroupSize > 0 ? GroupSize : length;
        Slots = [.. Enumerable.Range(0, length).Select(index => new OtpSlot(
            index < text.Length ? text[index].ToString() : string.Empty,
            IsKeyboardFocusWithin && index == Math.Min(text.Length, length - 1),
            index % group == 0,
            index % group == group - 1 || index == length - 1,
            index > 0 && index % group == 0))];
    }
}

public sealed class OtpModel(string code)
{
    public string Code { get; set; } = code;
}
