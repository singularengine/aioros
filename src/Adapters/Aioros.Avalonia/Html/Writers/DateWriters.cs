using Aioros.Avalonia.Html.Looks;
using Aioros.Avalonia.Plans;
using System.Globalization;
using System.Text;
using Aioros.Runtime.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace Aioros.Avalonia.Html.Writers;

internal static class DateWriters
{
    private static readonly (string Pseudo, string Selector)[] PickerStates = [(":pointerover", "hover"), (":focus-within", "focus")];

    public static void WriteCalendarDatePicker(HtmlContext context, CalendarDatePicker picker, ParentKind parent)
    {
        if (ThemedDatePickerWriters.Writes(picker))
        {
            ThemedDatePickerWriters.Write(context, picker, parent);
            return;
        }

        var look = ThemeLooks.Of(picker, "Background", PickerStates);
        var height = context.MinHeight(picker) + picker.BorderThickness.Top + picker.BorderThickness.Bottom;
        var range = Limit("min", picker.DisplayDateStart) + Limit("max", picker.DisplayDateEnd);
        WriteInput(context, new Input(picker, parent, look, height, "date"), CalendarDatePicker.SelectedDateProperty, Slots.FormatOf("date"), range);
    }

    public static void WriteDatePicker(HtmlContext context, DatePicker picker, ParentKind parent)
    {
        var range = string.Create(CultureInfo.InvariantCulture, $" min=\"{picker.MinYear.Year:D4}-01-01\" max=\"{picker.MaxYear.Year:D4}-12-31\"");
        WriteInput(context, new Input(picker, parent, FlyoutLook(picker), FlyoutHeight(picker), "date"), DatePicker.SelectedDateProperty, Slots.FormatOf("date"), range);
    }

    public static void WriteTimePicker(HtmlContext context, TimePicker picker, ParentKind parent)
    {
        var seconds = picker.UseSeconds;
        var step = seconds ? picker.SecondIncrement : picker.MinuteIncrement * 60;
        WriteInput(
            context,
            new Input(picker, parent, FlyoutLook(picker), FlyoutHeight(picker), "time"),
            TimePicker.SelectedTimeProperty,
            Slots.FormatOf("time", seconds ? "seconds" : "minutes"),
            string.Create(CultureInfo.InvariantCulture, $" step=\"{step}\""));
    }

    public static bool TryDate(string? text, out DateTime? date) => Runtime.Slots.FieldValues.TryDate(text, out date);

    public static bool TryTime(string? text, out TimeSpan? time) => Runtime.Slots.FieldValues.TryTime(text, out time);

    internal static string Date(object? value) => value switch
    {
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => string.Empty,
    };

    private static string Limit(string name, DateTime? date) => date is null ? string.Empty : $" {name}=\"{Date(date)}\"";

    private static void WriteInput(HtmlContext context, Input input, AvaloniaProperty property, Format format, string attributes)
    {
        var control = input.Control;
        var style = context.Box(control, input.Parent);
        BoxStyles.Font(style, control.FontSize, control.FontWeight, control.Foreground);
        style.Append(input.Look.Css);
        style.Append(CultureInfo.InvariantCulture, $"min-height:{CssValues.Px(input.Height)};");
        style.Append(double.IsNaN(control.Width) && control.HorizontalAlignment != HorizontalAlignment.Stretch ? "width:auto;" : string.Empty);
        var states = context.Sheet.States(input.Look);
        context.Plan.Literal($"<input type=\"{input.Type}\" name=\"f");
        context.Field(control);
        context.Plan.Literal("\" value=\"");
        RangeParts.Slot(context, control, property, format);
        context.Plan.Literal($"\"{attributes}");
        context.Root(control, states, style);
        context.Plan.Literal($"{RangeParts.Disabled(control)}>");
    }

    private static Look FlyoutLook(TemplatedControl picker)
    {
        if (RangeParts.Part<Button>(picker, "PART_FlyoutButton") is not { } button)
        {
            return Look.None;
        }

        var css = new StringBuilder();
        BoxStyles.Fill(css, button.Background, button.BorderBrush, button.BorderThickness, button.CornerRadius, new Thickness(0, 0, 0, 0));
        css.Append("padding:0 8px;");
        if (!picker.IsEffectivelyEnabled)
        {
            return new Look(css.ToString(), []);
        }

        var classes = (IPseudoClasses)button.Classes;
        classes.Add(":pointerover");
        var hover = $"background:{RangeParts.Color(button.Background)};border-color:{RangeParts.Color(button.BorderBrush)};";
        classes.Remove(":pointerover");
        return new Look(css.ToString(), [new LookState("hover", hover)]);
    }

    private static double FlyoutHeight(TemplatedControl picker) =>
        Math.Round(picker.FontSize * 1.21) + 11;

    private readonly record struct Input(TemplatedControl Control, ParentKind Parent, Look Look, double Height, string Type);
}
