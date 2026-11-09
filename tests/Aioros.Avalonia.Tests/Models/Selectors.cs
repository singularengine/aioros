using System.Collections.Immutable;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Metadata;

namespace Aioros.Avalonia.Tests.Models;

public enum Mood
{
    Calm,
    Busy,
    Away,
}

public sealed class Feeling(Mood mood, bool done, Person? who = null)
{
    public Mood Mood => mood;

    public bool Done => done;

    public Person? Who => who;
}

public sealed record Person(string Name);

public sealed class MoodIcons : IDataTemplate
{
    public static MoodIcons Instance { get; } = new();

    public Control? Build(object? param) => (param as Mood?) switch
    {
        Mood.Calm => new PathIcon { Data = Geometry.Parse("M0,0 L10,10"), Opacity = 0.5, Width = 14, Height = 14 },
        Mood.Busy => new PathIcon { Data = Geometry.Parse("M0,10 L10,0"), Foreground = Brushes.Red, [ToolTip.TipProperty] = "Busy" },
        _ => null,
    };

    public bool Match(object? data) => data is Mood;
}

public sealed class DoneMarks : IDataTemplate
{
    public Control? Build(object? param) => param is true ? new TextBlock { Text = "done" } : new TextBlock { Text = "open", FontStyle = FontStyle.Italic };

    public bool Match(object? data) => data is bool;
}

public sealed class NameTags : IDataTemplate
{
    public Control? Build(object? param) => new TextBlock { Text = (param as Person)?.Name };

    public bool Match(object? data) => data is Person;
}

public sealed record Notice(string Kind, string Text);

public sealed record Banner(string Title);

public sealed class Notices(params object[] items)
{
    public ImmutableArray<object> Items { get; } = [.. items];

    public object? Lead => Items.IsEmpty ? null : Items[0];
}

public sealed class NoticeTemplates : IDataTemplate
{
    [Content]
    public Dictionary<string, IDataTemplate> Templates { get; } = [];

    public IDataTemplate? DefaultTemplate { get; set; }

    public Control? Build(object? param) =>
        param is Notice notice && Templates.TryGetValue(notice.Kind, out var template) ? template.Build(param) : DefaultTemplate?.Build(param);

    public bool Match(object? data) => data is Notice or Banner;
}

public sealed class TunedTemplates : IDataTemplate
{
    public string Prefix { get; set; } = string.Empty;

    [Content]
    public Dictionary<string, IDataTemplate> Templates { get; } = [];

    public Control? Build(object? param) => param is Notice notice && Templates.TryGetValue(Prefix + notice.Kind, out var template) ? template.Build(param) : null;

    public bool Match(object? data) => data is Notice;
}
