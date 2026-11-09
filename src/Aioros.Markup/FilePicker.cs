using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using Avalonia.Styling;

namespace Aioros.Markup;

/// <summary>
/// A button that picks files into a view model property of Avalonia's own <see cref="IStorageFile"/> type: on the
/// desktop it opens the system file picker through <see cref="TopLevel.StorageProvider"/>, and on the web it is a file
/// input whose uploads reach the property as <see cref="IStorageFile"/>s read from the post.
/// </summary>
public sealed class FilePicker : Button
{
    /// <summary>The chosen file, for a picker of one file.</summary>
    public static readonly StyledProperty<IStorageFile?> FileProperty =
        AvaloniaProperty.Register<FilePicker, IStorageFile?>(nameof(File), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The chosen files, for a picker of several; binding it lets the picker choose more than one.</summary>
    public static readonly StyledProperty<IReadOnlyList<IStorageFile>?> FilesProperty =
        AvaloniaProperty.Register<FilePicker, IReadOnlyList<IStorageFile>?>(nameof(Files), defaultBindingMode: BindingMode.TwoWay);

    private TextBlock? _name;

    private bool? _implicit;

    private string? _none;

    /// <summary>The chosen file, for a picker of one file.</summary>
    public IStorageFile? File
    {
        get => GetValue(FileProperty);
        set => SetValue(FileProperty, value);
    }

    /// <summary>The chosen files, for a picker of several.</summary>
    public IReadOnlyList<IStorageFile>? Files
    {
        get => GetValue(FilesProperty);
        set => SetValue(FilesProperty, value);
    }

    /// <summary>The file types offered: the system picker's filter on the desktop, the input's <c>accept</c> on the web.</summary>
    public AvaloniaList<FilePickerFileType> FileTypes { get; } = [];

    /// <summary>Whether the picker chooses several files: its <see cref="Files"/> is bound or set.</summary>
    public bool AllowsMultiple => IsSet(FilesProperty);

    /// <summary>
    /// A button's style key, so the picker looks like the theme's buttons, unless its theme is made for file pickers or
    /// the resources around it hold an implicit one (a library's file input), which Avalonia applies only to a control of
    /// that style key.
    /// </summary>
    protected override Type StyleKeyOverride =>
        (Theme?.TargetType is { } target && typeof(FilePicker).IsAssignableFrom(target)) || Implicit ? typeof(FilePicker) : typeof(Button);

    private bool Implicit => _implicit ?? (((ILogical)this).LogicalParent is not null && (_implicit = this.TryFindResource(typeof(FilePicker), out var theme) && theme is ControlTheme).Value);

    /// <summary>Looks for an implicit file picker theme again where the picker is attached next.</summary>
    /// <param name="e">The event.</param>
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        _implicit = null;
    }

    /// <summary>Finds the theme's <c>PART_FileName</c> text, whose own text is shown while no file is chosen.</summary>
    /// <param name="e">The applied template.</param>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _name = e.NameScope.Find<TextBlock>("PART_FileName");
        _none = _name?.Text;
        ShowNames();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FileProperty || change.Property == FilesProperty)
        {
            ShowNames();
        }
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        base.OnClick();
        if (TopLevel.GetTopLevel(this)?.StorageProvider is { CanOpen: true } storage)
        {
            _ = PickAsync(storage.OpenFilePickerAsync);
        }
    }

    internal async Task PickAsync(Func<FilePickerOpenOptions, Task<IReadOnlyList<IStorageFile>>> open)
    {
        var chosen = await open(new FilePickerOpenOptions
        {
            Title = Content as string,
            AllowMultiple = AllowsMultiple,
            FileTypeFilter = FileTypes.Count == 0 ? null : [.. FileTypes],
        });
        if (chosen.Count == 0)
        {
            return;
        }

        if (AllowsMultiple)
        {
            Files = chosen;
        }
        else
        {
            File = chosen[0];
        }
    }

    private void ShowNames()
    {
        if (_name is null)
        {
            return;
        }

        var chosen = AllowsMultiple ? Files ?? [] : File is { } file ? [file] : [];
        if (chosen.Count > 0)
        {
            _name.Text = string.Join(", ", chosen.Select(file => file.Name));
            return;
        }

        _name.ClearValue(TextBlock.TextProperty);
        if (!_name.IsSet(TextBlock.TextProperty))
        {
            _name.Text = _none;
        }
    }
}
