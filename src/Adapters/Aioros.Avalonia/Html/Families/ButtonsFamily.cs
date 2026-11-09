using System.Collections.Immutable;
using Aioros.Avalonia.Forms;
using Aioros.Avalonia.Html.Writers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Aioros.Avalonia.Html.Families;

internal static class ButtonsFamily
{
    public static ImmutableArray<IControlWriter> Writers { get; } =
    [
        new ControlWriter<Aioros.Markup.FilePicker>(FilePickerWriters.WriteFilePicker) { Rooted = true },
        new ControlWriter<ToggleSwitch>(ToggleWriters.WriteToggleSwitch) { Rooted = true },
        new ControlWriter<CheckBox>(ToggleWriters.WriteThreeState, check => check.IsThreeState) { Rooted = true },
        new ControlWriter<CheckBox>(InputWriters.WriteCheckBox) { Rooted = true },
        new ControlWriter<RadioButton>(ToggleWriters.WriteRadioButton) { Rooted = true },
        new ControlWriter<ToggleButton>(ToggleWriters.WriteToggleButton) { Rooted = true },
        new ControlWriter<HyperlinkButton>(ButtonWriters.WriteLink) { Rooted = true },
        new ControlWriter<DropDownButton>(SplitWriters.WriteDropDown) { Rooted = true },
        new ControlWriter<RepeatButton>(ButtonWriters.WriteButton) { Rooted = true },
        new ControlWriter<Button>(ButtonWriters.WriteButton) { Rooted = true },
        new ControlWriter<SplitButton>(SplitWriters.WriteSplitButton) { Rooted = true },
        new ControlWriter<ButtonSpinner>(SplitWriters.WriteButtonSpinner) { Rooted = true },
    ];

    public static ImmutableArray<IFieldBinder> Binders { get; } =
    [
        new FieldBinder<CheckBox>(ToggleWriters.BindCheckBox),
        new FieldBinder<RadioButton>(ToggleWriters.BindRadio),
        new FieldBinder<ToggleButton>((toggle, value) => toggle.IsChecked = toggle.IsEffectivelyEnabled ? value is not null : toggle.IsChecked),
        new FieldBinder<ToggleSplitButton>((toggle, value) => toggle.IsChecked = toggle.IsEffectivelyEnabled ? value is not null : toggle.IsChecked),
        new FieldBinder<ButtonSpinner>(SplitWriters.BindSpinner),
    ];
}
