using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using Gallery.Models;

namespace Gallery.DataTemplates;

/// <summary>
/// Picks a notice's template by its kind, as Avalonia's "How to implement a data template selector" shows: the templates
/// are given in XAML by key, and the selector looks the kind up among them.
/// </summary>
public sealed class NoticeTemplateSelector : IDataTemplate
{
    [Content]
    public Dictionary<string, IDataTemplate> AvailableTemplates { get; } = [];

    public Control? Build(object? param) => param is Notice notice ? AvailableTemplates[notice.Kind].Build(param) : null;

    public bool Match(object? data) => data is Notice notice && AvailableTemplates.ContainsKey(notice.Kind);
}
