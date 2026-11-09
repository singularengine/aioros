using System.Collections.Immutable;
using Aioros.Avalonia.Forms;
using Aioros.Avalonia.Html.Writers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Aioros.Avalonia.Html.Families;

internal static class TextFamily
{
    public static ImmutableArray<IControlWriter> Writers { get; } =
    [
        new ControlWriter<SelectableTextBlock>(TextWriters.WriteSelectable) { Rooted = true },
        new ControlWriter<AccessText>(TextWriters.WriteAccessText) { Rooted = true },
        new ControlWriter<TextBlock>(TextWriters.WriteTextBlock) { Rooted = true },
        new ControlWriter<Label>(TextWriters.WriteLabel) { Rooted = true },
        new ControlWriter<MaskedTextBox>(TextInputWriters.WriteMaskedTextBox, box => box.MaskProvider is not null) { Rooted = true },
        new ControlWriter<TextBox>(TextInputWriters.WriteTextBox) { Rooted = true },
        new ControlWriter<NumericUpDown>(TextInputWriters.WriteNumericUpDown) { Rooted = true },
        new ControlWriter<AutoCompleteBox>(TextInputWriters.WriteAutoCompleteBox) { Rooted = true },
        new ControlWriter<ColorPicker>(ColorWriters.WriteColorPicker) { Rooted = true },
    ];

    public static ImmutableArray<IFieldBinder> Binders { get; } =
    [
        new FieldBinder<TextBox>((box, value) =>
        {
            box.Text = value ?? box.Text;
            DatePickerPosts.Typed(box);
        }),
        new FieldBinder<NumericUpDown>(TextInputWriters.BindNumber),
        new FieldBinder<AutoCompleteBox>((box, value) => box.Text = value ?? box.Text),
        new FieldBinder<ColorPicker>(ColorWriters.BindColor),
    ];
}
