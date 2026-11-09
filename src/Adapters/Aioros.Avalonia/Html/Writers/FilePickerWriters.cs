using Aioros.Markup;
using System.Collections.Immutable;
using Aioros.Avalonia.Controls;
using Aioros.Avalonia.Html.Looks;
using Aioros.Runtime.Rendering;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace Aioros.Avalonia.Html.Writers;

internal static class FilePickerWriters
{

    public static void WriteFilePicker(HtmlContext context, FilePicker picker, ParentKind parent)
    {
        var style = context.Box(picker, parent);
        BoxStyles.Font(style, picker.FontSize, picker.FontWeight, null);
        picker.ApplyTemplate();
        var parts = ButtonWriters.Parts(picker);
        var look = ThemeLooks.Of(picker, parts is Border { Name: { Length: > 0 } root } ? root : "PART_ContentPresenter", ThemeLooks.PressStates);
        style.Append(look.Css);
        ButtonWriters.Bare(style, parts);
        style.Append(context.MinHeight(picker) > 0 ? $"min-height:{CssValues.Px(context.MinHeight(picker))};" : string.Empty);
        style.Append(parts is null ? "position:relative;display:inline-flex;align-items:center;justify-content:center;cursor:pointer;" : "position:relative;display:flex;cursor:pointer;" + ButtonWriters.ContentAlignment(picker, parts));
        ToggleWriters.AddHidden(context);
        context.Sheet.AddRule("fp", "label:has(>input[type=file]:focus-visible){outline:2px solid currentColor;outline-offset:2px}\nlabel[data-chosen]:not(:has([data-aioros-file-name]))::after{content:attr(data-chosen);margin-left:8px;opacity:.75}\n");
        context.Plan.Literal("<label");
        context.Root(picker, context.Sheet.States(look), style, attributes: false);
        context.Plan.Literal(">");
        if (parts?.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(text => text.Name == "PART_FileName") is { } name)
        {
            context.FileNames.Add(name);
        }

        ButtonWriters.WriteInside(context, picker, parts);
        context.Plan.Follow(picker, FilePicker.FileProperty);
        context.Plan.Follow(picker, FilePicker.FilesProperty);
        context.Plan.Literal("<input type=\"file\"");
        context.Attributes(picker);
        context.Plan.Literal(" name=\"f");
        context.Field(picker);
        var accept = Accept(picker.FileTypes);
        context.Plan.Literal(string.Concat(
            "\" class=\"vh\"",
            picker.AllowsMultiple ? " multiple" : string.Empty,
            accept is null ? string.Empty : $" accept=\"{CssValues.Encode(accept)}\"",
            picker.IsEffectivelyEnabled ? string.Empty : " disabled",
            "></label>"));
    }

    /// <summary>The input's <c>accept</c>: each pattern as an extension and each MIME type once, or nothing when a type takes every file.</summary>
    public static string? Accept(IEnumerable<FilePickerFileType> types)
    {
        var accepted = new List<string>();
        foreach (var type in types)
        {
            foreach (var pattern in type.Patterns ?? [])
            {
                if (pattern is "*" or "*.*")
                {
                    return null;
                }

                if (pattern.StartsWith("*.", StringComparison.Ordinal))
                {
                    accepted.Add(pattern[1..]);
                }
            }

            accepted.AddRange(type.MimeTypes ?? []);
        }

        return accepted.Count == 0 ? null : string.Join(',', accepted.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The files a post carries for the picker, given as Avalonia's own storage files over their temporary copies (the
    /// storage interfaces cannot be implemented outside Avalonia); a post without one leaves the picker as it is.
    /// </summary>
    public static async Task BindAsync(FilePicker picker, IStorageProvider storage, ImmutableArray<PostedFile> files)
    {
        if (files.IsDefaultOrEmpty || !picker.IsEffectivelyEnabled)
        {
            return;
        }

        var chosen = new List<IStorageFile>(files.Length);
        foreach (var file in files)
        {
            if (await storage.TryGetFileFromPathAsync(file.Path) is { } found)
            {
                chosen.Add(found);
            }
        }

        if (picker.AllowsMultiple)
        {
            picker.Files = chosen;
        }
        else if (chosen.Count > 0)
        {
            picker.File = chosen[0];
        }
    }
}
